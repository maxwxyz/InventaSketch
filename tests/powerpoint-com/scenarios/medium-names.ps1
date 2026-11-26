# Scenario (plan review 2026-11-25 night, items 11 and 12): colour by medium - wires with conductor L1/N/PE, heating flow and
# return pipes and a blue cabinet core; More > Colour by medium recolours the lines (IEC 60445 colours), keeps the core, a
# legend lists the colours, "Off" restores; the halo still works. Names across slides - a wire "W3" at the right slide edge
# says where it continues ("W3, slide 2") once slide 2 has a W3 too, and "Select connected > Same name" reports it.
param($S)
$addin = $S.AddIn
$pres = $S.Slide.Parent

function Hex([int]$rgb) { '#{0:x2}{1:x2}{2:x2}' -f ($rgb -band 255), (($rgb -shr 8) -band 255), (($rgb -shr 16) -band 255) }
# Line colours (#rrggbb, sorted) of the native shapes of a unit.
function LineColours($shape) {
    $items = if ($shape.Type -eq 6) { @($shape.GroupItems) } else { @($shape) }
    $c = @()
    foreach ($i in $items) { try { if ($i.Line.Visible) { $c += (Hex ([int]$i.Line.ForeColor.RGB)) } } catch { } }
    @($c | Sort-Object -Unique)
}
# Texts of the native shapes of a unit.
function Texts($shape) {
    $items = if ($shape.Type -eq 6) { @($shape.GroupItems) } else { @($shape) }
    $t = @()
    foreach ($i in $items) { try { if ($i.HasTextFrame -and $i.TextFrame.HasText) { $t += [string]$i.TextFrame.TextRange.Text } } catch { } }
    @($t)
}
function SelectedName { try { $S.Window.Selection.ShapeRange.Item(1).Name } catch { '' } }
function Get-LogTail([string]$pattern) { Get-Content (Get-IskLog $S) | Where-Object { $_ -match $pattern } | Select-Object -Last 1 }
# Inserts an item on the current slide, places it and selects it; returns the shape name (it changes when redrawn).
function Put([string]$item, [double]$left, [double]$top, $slide) {
    $n = $addin.Insert($item)
    $sh = $slide.Shapes.Item($n); $sh.Left = $left; $sh.Top = $top
    Start-Sleep 1
    try { $slide.Shapes.Item($n).Select() } catch { Start-Sleep 1; $slide.Shapes.Item($n).Select() }
    Start-Sleep 2
    $n
}
# Runs a command on the selected unit and returns the name of the redrawn unit (selected afterwards).
function Run([string]$cmd, [string]$value) {
    if ($value) { $addin.CommandWithInput($cmd, $value) | Out-Null } else { $addin.Command($cmd) | Out-Null }
    Start-Sleep 3
    SelectedName
}
function Shape($slide, [string]$name) { try { $slide.Shapes.Item($name) } catch { $null } }

# ---------------------------------------------------------------- 1) colour by medium
$l1 = Put 'elec.wire' 120 80 $S.Slide
$l1 = Run 'act:line.role=L1'
$n = Put 'elec.wire' 120 130 $S.Slide
$n = Run 'act:line.role=N'
$pe = Put 'elec.wire' 120 180 $S.Slide
$pe = Run 'act:line.role=PE'
$flow = Put 'bs.pipe.flow' 380 80 $S.Slide
$ret = Put 'bs.pipe.return' 380 130 $S.Slide
$core = Put 'sw.line.wire.bu' 380 180 $S.Slide
$S.Window.Selection.Unselect(); Start-Sleep 1
$before = LineColours (Shape $S.Slide $l1)
$coreBefore = (LineColours (Shape $S.Slide $core)) -join ','
Write-IskInfo ("L1 wire before: " + ($before -join ','))
Assert-Isk $S (-not ($before -contains '#8d5524')) 'without the colour mode the L1 wire is drawn as before (not brown)'

Assert-Isk $S ($addin.SetColorBy('medium')) 'Colour by medium is switched on'
Start-Sleep 4
$units = @(Get-IskShapes $S)
function ByType([string]$pattern) { @($units | Where-Object { $_.Name -like $pattern }) }
$wires = ByType 'ISK:elec.wire*'
$all = @($wires | ForEach-Object { LineColours $_ }) | Sort-Object -Unique
Write-IskInfo ("wire colours: " + ($all -join ','))
Assert-Isk $S ($all -contains '#8d5524') 'L1 is brown (IEC 60445)'
Assert-Isk $S ($all -contains '#2f8fdd') 'N is blue (IEC 60445)'
Assert-Isk $S (($all -contains '#2e9d3a') -and ($all -contains '#f2d500')) 'PE is green-yellow (IEC 60445)'
$pipes = @(ByType 'ISK:bs.pipe*' | ForEach-Object { LineColours $_ }) | Sort-Object -Unique
Write-IskInfo ("pipe colours: " + ($pipes -join ','))
Assert-Isk $S (($pipes -contains '#d32f2f') -and ($pipes -contains '#1976d2')) 'heating flow warm red, return cold blue'
$coreNow = (@(ByType 'ISK:sw.line.wire*' | ForEach-Object { LineColours $_ }) | Sort-Object -Unique) -join ','
Assert-Isk $S ($coreNow -eq $coreBefore) "a cabinet core keeps its real colour ($coreBefore -> $coreNow)"
Assert-Isk $S ($S.Slide.Tags.Item('ISK_STYLE') -like '*colors=medium*') 'the slide style keeps the colour mode'
# A new element on the slide takes it too.
$l3 = Put 'elec.wire' 120 230 $S.Slide
$l3 = Run 'act:line.role=L3'
Assert-Isk $S ((LineColours (Shape $S.Slide $l3)) -contains '#8c8c8c') 'a new wire set to L3 is grey at once'
# The halo still works on a recoloured line.
$S.Window.Selection.Unselect(); Start-Sleep 1
(Shape $S.Slide $l3).Select(); Start-Sleep 3
$halo = Get-LogTail 'Halo: '
Write-IskInfo ('halo: ' + $halo)
Assert-Isk $S ($halo -match [regex]::Escape($l3)) 'the selected recoloured wire glows (halo)'
$S.Window.Selection.Unselect(); Start-Sleep 1
$legend = $addin.InsertLegend(); Start-Sleep 3
Assert-Isk $S ($legend -ne '') 'a legend of the used colours is inserted'
if ($legend) {
    $lt = (Texts (Shape $S.Slide $legend)) -join ' | '
    Write-IskInfo ('legend: ' + $lt)
    Assert-Isk $S ($lt -match 'L1' -and $lt -match 'IEC 60445') 'the legend names L1 with its standard'
}
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '1-colour-by-medium'

Assert-Isk $S ($addin.SetColorBy('')) 'Colour by medium is switched off'
Start-Sleep 4
$units = @(Get-IskShapes $S)
$off = @(ByType 'ISK:elec.wire*' | ForEach-Object { LineColours $_ }) | Sort-Object -Unique
Write-IskInfo ("wire colours off: " + ($off -join ','))
Assert-Isk $S (-not ($off -contains '#8d5524') -and -not ($off -contains '#2e9d3a')) 'off restores the own colours'
Save-IskShot $S '2-colour-off'

# ---------------------------------------------------------------- 2) names across slides
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
Start-Sleep 1
$slideW = $pres.PageSetup.SlideWidth
# A wire W3 whose right end is at the slide edge.
$w = Put 'elec.wire' 300 200 $S.Slide
$sh = Shape $S.Slide $w; $sh.Left = $slideW - $sh.Width; Start-Sleep 2
# The add-in may redraw a moved unit: select it again by name.
try { (Shape $S.Slide $w).Select() } catch { Start-Sleep 2; (Shape $S.Slide $w).Select() }
Start-Sleep 2
$w = Run 'act:line.ref' 'W3'
$t = (Texts (Shape $S.Slide $w)) -join ' | '
Write-IskInfo ('slide 1 wire texts: ' + $t)
Assert-Isk $S ($t -match 'W3') 'the wire shows its designation W3'
$arrow = [string][char]0x2192
Assert-Isk $S ($t.Contains($arrow + ' W3')) 'at the slide edge it shows a continuation marker'
# Slide 2 with the continuation W3.
$s2 = $pres.Slides.Add(2, 12)
$S.Window.View.GotoSlide(2); Start-Sleep 2
$w2 = Put 'elec.wire' 80 200 $s2
$w2 = Run 'act:line.ref' 'W3'
$t2 = (Texts (Shape $s2 $w2)) -join ' | '
Write-IskInfo ('slide 2 wire texts: ' + $t2)
Assert-Isk $S ($t2 -match 'W3, slide 1|W3, Folie 1') 'on slide 2 the wire says it continues on slide 1'
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '3-slide2-continuation'
# Back on slide 1: end markers on (a redraw) - the marker now names slide 2; Same name reports slide 2.
$S.Window.View.GotoSlide(1); Start-Sleep 2
(Shape $S.Slide $w).Select(); Start-Sleep 2
$w = Run 'act:line.ref.ends'
$t = (Texts (Shape $S.Slide $w)) -join ' | '
Write-IskInfo ('slide 1 wire texts: ' + $t)
Assert-Isk $S ($t -match 'W3, slide 2|W3, Folie 2') 'on slide 1 the marker names slide 2'
Assert-Isk $S (@(Texts (Shape $S.Slide $w) | Where-Object { $_ -eq 'W3' }).Count -ge 2) 'end markers and the label show W3'
$else = [string]$addin.SameNameElsewhere()
Write-IskInfo ('same name elsewhere: ' + $else)
Assert-Isk $S ($else -eq 'W3=2') 'the add-in knows W3 is also on slide 2'
# A second W3 on slide 1: Same name selects both and reports slide 2.
$w4 = Put 'elec.wire' 120 300 $S.Slide
$w4 = Run 'act:line.ref' 'w3'
$S.Window.Selection.Unselect(); Start-Sleep 1
(Shape $S.Slide $w).Select(); Start-Sleep 3
$ids = @(([string]$addin.CommandIds()) -split ',')
Assert-Isk $S ($ids -contains 'connect:samename') 'the wire offers "Select connected > Same name"'
$addin.Command('connect:samename') | Out-Null; Start-Sleep 3
$sel = try { $S.Window.Selection.ShapeRange.Count } catch { 0 }
$log = Get-LogTail 'Select connected \(samename\)'
Write-IskInfo ('selected: ' + $sel + '; ' + $log)
Assert-Isk $S ($sel -eq 2) 'Same name selects both W3 wires on this slide'
Assert-Isk $S ($log -match 'also on slide 2|auch auf Folie 2') 'and reports W3 on slide 2'
Save-IskShot $S '4-same-name'
$S.Window.Selection.Unselect()
