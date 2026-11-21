# Scenario: two crossing wires: the upper one hops, the connect icon at the crossing joins them.
param($S)
$addin = $S.AddIn
$k = Get-IskScale

$a = $addin.Insert('elec.wire.free')
$b = $addin.Insert('elec.wire.free')
$S.Slide.Shapes.Item($b).Select(); Start-Sleep 2
$addin.Command('rot90') | Out-Null; Start-Sleep 4
Write-IskShapes $S
# Select the horizontal wire: the connect icon sits above right of the crossing.
$sa = Get-IskShapes $S 'ISK:elec.wire*' | Where-Object { $_.Height -lt 1 } | Select-Object -First 1
$sa.Select(); Start-Sleep 3
$r = Get-IskRect $S $sa.Name
Save-IskShot $S '1-crossing' -Around ($r.CX - 250), ($r.Y - 200), 500, 400
$before = @(Get-IskShapes $S 'ISK:elec.wire*').Count
Invoke-IskClick ($r.CX + 13 * $k) ($r.Y - 13 * $k) -WaitMs 4000
$after = @(Get-IskShapes $S 'ISK:elec.wire*').Count
Write-IskShapes $S
Assert-Isk $S ($after -ne $before) "the connect icon joins the wires at the crossing ($before -> $after wires)"
Save-IskShot $S '2-connected' -Around ($r.CX - 250), ($r.Y - 200), 500, 400
