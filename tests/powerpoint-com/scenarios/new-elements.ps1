# Scenario (2026-11-25): the optics module and the digital timing diagram insert as native shapes in PowerPoint; a laser
# with a docked ray and a lens draws the traced beam.
param($S)
$addin = $S.AddIn

function Insert-At($id, $x, $y) {
    $S.Window.Selection.Unselect()
    $n = $addin.Insert($id); $sh = $S.Slide.Shapes.Item($n)
    $sh.Left = $x; $sh.Top = $y
    $sh
}

$timing = Insert-At 'chart.timing.bus' 40 40
Assert-Isk $S ((Get-IskModel $timing) -match 'chart\.timing') 'the timing diagram inserts'
Assert-Isk $S ($timing.Type -eq 6 -and $timing.GroupItems.Count -ge 5) 'the timing diagram is drawn natively'
$keys = @(); foreach ($c in $timing.GroupItems) { $keys += $c.Tags.Item('ISK_PART') }
Write-IskInfo ("timing parts: " + (($keys | Sort-Object -Unique) -join ', '))
Assert-Isk $S ($keys -contains 'signal.0') 'each signal is a part'

$ids = @('optics.lens.biconvex', 'optics.mirror.concave', 'optics.prism', 'optics.source.laser', 'optics.detector.eye', 'optics.lens.converging.symbol')
$x = 40
foreach ($id in $ids) {
    $sh = Insert-At $id $x 330
    Assert-Isk $S ((Get-IskModel $sh) -match [regex]::Escape(($id -split '\.')[0..1] -join '.')) "$id inserts"
    $x += 105
}
Save-IskShot $S '1-timing-and-optics'
Send-IskEscape; Start-Sleep 1
