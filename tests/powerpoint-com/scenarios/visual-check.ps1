# Visual check (2026-11-25): bar pictograms of typical elements, a click right on a chart curve selects the curve,
# the pie in one text size, the calculator window.
param($S)
$addin = $S.AddIn
function Shot-Unit($name, $tag) {
    $r = Get-IskRect $S $name
    Save-IskShot $S $tag -Around ($r.X - 220), ($r.Y - 200), ($r.W + 440), ($r.H + 400)
}
# 1. Bars of a gear, a cable, a resistor, a map route.
$x = 120
foreach ($id in @('mech.spur.axial', 'elec.resistor', 'map.route', 'kin.fourbar')) {
    $S.Window.Selection.Unselect()
    $n = $addin.Insert($id); $sh = $S.Slide.Shapes.Item($n); $sh.Left = 300; $sh.Top = 220
    $sh.Select(); Start-Sleep 2
    Shot-Unit $n ("1-bar-" + $id)
    $sh.Delete(); Start-Sleep 1
}
# 2. A click right on the curve of a line chart selects the curve at once.
$S.Window.Selection.Unselect(); Start-Sleep 1
$c = $addin.Insert('chart.line'); $cs = $S.Slide.Shapes.Item($c); $cs.Left = 200; $cs.Top = 120; $cs.Width = 420; $cs.Height = 280
$S.Window.Selection.Unselect(); Start-Sleep 1
$curve = $null
foreach ($l in $cs.GroupItems) { if ($l.Tags.Item('ISK_PART') -eq 'series.0') { $curve = $l } }
$node = $curve.Nodes.Item([int][math]::Max(1, [math]::Floor($curve.Nodes.Count / 2))).Points
$flat = @($node | ForEach-Object { $_ })
$scr = ConvertTo-IskPixel $S @(([double]$flat[0] * 25.4 / 72), ([double]$flat[1] * 25.4 / 72))
Invoke-IskClick $scr[0] $scr[1] 2500
$part = $addin.SelectedPart()
Write-IskInfo ("first click on the curve selects: '$part'")
Assert-Isk $S ($part -eq 'series.0') 'the first click on the curve selects the curve'
Shot-Unit $c '2-curve-first-click'
# 3. The pie of the chart part and of a gear.
$r = Get-IskRect $S $c
Move-IskMouse $scr[0] $scr[1]; Start-Sleep -Milliseconds 400
[IskTest.Native]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100
[IskTest.Native]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 1300
Save-IskShot $S '3-pie-curve' -Around ($scr[0] - 380), ($scr[1] - 380), 760, 820
Send-IskEscape; Start-Sleep 1
