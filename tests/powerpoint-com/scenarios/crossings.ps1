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

# Back to a bridge (maintainer 2026-11-26): select a wire part at the junction, its bridge handle splits the junction again.
$S.Window.Selection.Unselect(); Start-Sleep 1
$parts = @(Get-IskShapes $S 'ISK:elec.wire*')
$bridged = $false
foreach ($p in $parts) {
    $p.Select(); Start-Sleep 3
    $pr = Get-IskRect $S $p.Name
    Move-IskMouse ($pr.CX) ($pr.CY); Start-Sleep 1
    $hs = [string]$addin.Handles()
    $h = ($hs -split ';') | Where-Object { $_ -like 'bridge:*' } | Select-Object -First 1
    if (-not $h) { continue }
    $xy = ($h -split '@')[1] -split ','
    Move-IskMouse ([double]$xy[0]) ([double]$xy[1]); Start-Sleep 1
    Save-IskShot $S '3-bridge-handle' -Around ($r.CX - 250), ($r.Y - 200), 500, 400
    Invoke-IskClick ([double]$xy[0]) ([double]$xy[1]) -WaitMs 4000
    $bridged = $true
    break
}
Write-IskShapes $S
Assert-Isk $S $bridged 'a wire part at the junction shows the bridge handle'
$again = @(Get-IskShapes $S 'ISK:elec.wire*').Count
Assert-Isk $S ($again -eq $before) "the bridge handle splits the junction into a crossing again ($after -> $again wires)"
Save-IskShot $S '4-bridge-again' -Around ($r.CX - 250), ($r.Y - 200), 500, 400
