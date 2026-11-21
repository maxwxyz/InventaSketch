# Scenario: motion mode on a planetary stage: turning the sun by 90 deg turns the carrier with its planets (fixed ring).
param($S)
$addin = $S.AddIn

$c = $addin.Insert('mech.carrier.axial')
$cs = $S.Slide.Shapes.Item($c); $cs.Left = 360; $cs.Top = 130; $cs.Width = 230; $cs.Height = 230
$cs.Select(); Start-Sleep 3
$addin.Command('port:hub:mech.spur.teeth') | Out-Null; Start-Sleep 4
$cs = @(Get-IskShapes $S 'ISK:mech.carrier*')[0]
foreach ($pin in 'pin0', 'pin1', 'pin2') { $cs.Select(); Start-Sleep 2; $addin.Command("port:${pin}:mech.spur.teeth") | Out-Null; Start-Sleep 4 }
$cs.Select(); Start-Sleep 2
$addin.Command('port:hub:mech.ring.teeth') | Out-Null; Start-Sleep 4
Write-IskShapes $S

$ccx = $cs.Left + $cs.Width / 2; $ccy = $cs.Top + $cs.Height / 2
$spurs = @(Get-IskShapes $S 'ISK:mech.gear.spur*')
$sun = $spurs | Sort-Object { [Math]::Abs(($_.Left + $_.Width / 2) - $ccx) + [Math]::Abs(($_.Top + $_.Height / 2) - $ccy) } | Select-Object -First 1
function Angles { Get-IskShapes $S 'ISK:mech.gear.spur*' | Where-Object { [Math]::Abs(($_.Left + $_.Width / 2) - $ccx) + [Math]::Abs(($_.Top + $_.Height / 2) - $ccy) -gt 5 } | ForEach-Object { [Math]::Atan2(($_.Top + $_.Height / 2) - $ccy, ($_.Left + $_.Width / 2) - $ccx) * 180 / [Math]::PI } | Sort-Object }
$before = @(Angles)
$sun.Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('motion')) 'motion mode switches on'
Start-Sleep 2
$r = Get-IskRect $S $sun.Name
Invoke-IskArcDrag -Center @($r.CX, $r.CY) -Radius ($r.W * 0.3) -Start 0 -Sweep ([Math]::PI / 2) -Steps 18 -BeforeRelease { Save-IskShot $S '1-dragging' -Around ($r.CX - 500), ($r.CY - 450), 1000, 900 }
Start-Sleep 2
$after = @(Angles)
$turn = @(for ($i = 0; $i -lt [Math]::Min($before.Count, $after.Count); $i++) { $d = $after[$i] - $before[$i]; while ($d -lt -180) { $d += 360 }; while ($d -gt 180) { $d -= 360 }; $d })
Write-IskInfo ('planet angles {0} -> {1}' -f ($before -join ', '), ($after -join ', '))
$mean = ($turn | Measure-Object -Average).Average
# Fixed ring: the carrier turns by z_sun / (z_sun + z_ring) of the sun's turn, about a quarter here (24.9 deg measured 2026-11-21).
Assert-Isk $S ($before.Count -eq 3 -and [Math]::Abs($mean) -gt 15 -and [Math]::Abs($mean) -lt 35) ('turning the sun by 90 deg turns the carrier by {0:0.0} deg' -f $mean)
$addin.Command('motion') | Out-Null
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-after' -Around ($r.CX - 500), ($r.CY - 450), 1000, 900
