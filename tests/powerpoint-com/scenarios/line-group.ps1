# Scenario: the ISO 128 line group as a slide style – all elements get thicker lines, new ones too, and back.
param($S)
$addin = $S.AddIn
# PowerPoint drops an invalid ribbon XML silently: the tab must be there.
Assert-Isk $S ($addin.RibbonLoaded()) 'the InventaSketch ribbon tab is loaded'

# Widest outline (pt) of the native shapes of a unit (group items or the shape itself).
function MaxWeight($shape) {
    $items = if ($shape.Type -eq 6) { @($shape.GroupItems) } else { @($shape) }
    $w = 0.0
    foreach ($i in $items) { try { if ($i.Line.Visible -and $i.Line.Weight -gt $w) { $w = [double]$i.Line.Weight } } catch { } }
    $w
}

$g1 = $addin.Insert('mech.spur.axial'); $s1 = $S.Slide.Shapes.Item($g1); $s1.Left = 80; $s1.Top = 80
Start-Sleep 2
$before = MaxWeight (@(Get-IskShapes $S 'ISK:mech.gear.spur*')[0])
Write-IskInfo ("widest line before: {0:0.00} pt" -f $before)
Assert-Isk $S ($addin.SetLineGroup(1.0)) 'the line group 1 mm is set'
Start-Sleep 3
$after = MaxWeight (@(Get-IskShapes $S 'ISK:mech.gear.spur*')[0])
Write-IskInfo ("widest line at 1 mm: {0:0.00} pt" -f $after)
Assert-Isk $S ([Math]::Abs($after - 2 * $before) -lt 0.2) 'the lines are twice as wide at line group 1 mm'
Assert-Isk $S ($S.Slide.Tags.Item('ISK_STYLE') -like '*group=1*') 'the slide keeps the style in its tag'

# A new element on the slide takes the slide style.
$S.Window.Selection.Unselect()
$g2 = $addin.Insert('mech.spur.axial'); $s2 = $S.Slide.Shapes.Item($g2); $s2.Left = 320; $s2.Top = 80
Start-Sleep 3
$new = MaxWeight $S.Slide.Shapes.Item($g2)
Write-IskInfo ("widest line of the new gear: {0:0.00} pt" -f $new)
Assert-Isk $S ([Math]::Abs($new - $after) -lt 0.2) 'a newly inserted element takes the slide line group'
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '1-line-group-1mm'

Assert-Isk $S ($addin.SetLineGroup(0.5)) 'back to the standard line group'
Start-Sleep 3
$back = MaxWeight (@(Get-IskShapes $S 'ISK:mech.gear.spur*')[0])
Assert-Isk $S ([Math]::Abs($back - $before) -lt 0.2) 'the standard line group restores the line widths'

# Accent colour (slide style): the coloured parts of an element take the accent, "" restores the original colours.
function FillColours($shape) {
    $items = if ($shape.Type -eq 6) { @($shape.GroupItems) } else { @($shape) }
    $c = @()
    foreach ($i in $items) { try { if ($i.Fill.Visible) { $c += [int]$i.Fill.ForeColor.RGB } } catch { } }
    ($c | Sort-Object -Unique) -join ','
}
$S.Window.Selection.Unselect()
$bat = $addin.Insert('energy.storage.battery'); $bs = $S.Slide.Shapes.Item($bat); $bs.Left = 520; $bs.Top = 80
Start-Sleep 2
$orig = FillColours (@(Get-IskShapes $S 'ISK:energy.storage.battery*')[0])
Assert-Isk $S ($addin.SetAccent('red')) 'the accent colour red is set'
Start-Sleep 3
$red = FillColours (@(Get-IskShapes $S 'ISK:energy.storage.battery*')[0])
Write-IskInfo ("battery fills: {0}  ->  {1}" -f $orig, $red)
Assert-Isk $S ($red -ne $orig) 'the coloured parts take the accent colour'
Assert-Isk $S ($S.Slide.Tags.Item('ISK_STYLE') -like '*accent=#c62828*') 'the slide style keeps the accent'
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '2-accent-red'
Assert-Isk $S ($addin.SetAccent('')) 'back to the original colours'
Start-Sleep 3
Assert-Isk $S ((FillColours (@(Get-IskShapes $S 'ISK:energy.storage.battery*')[0])) -eq $orig) 'the original colours come back'
