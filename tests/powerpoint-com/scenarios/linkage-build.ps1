# Scenario: a four-bar built with the build "+" (links, fixed pivots) and dragged in motion mode with constant lengths.
param($S)
$addin = $S.AddIn
function Lengths { Get-IskShapes $S 'ISK:kin.link*' | ForEach-Object { $p = Get-IskPoints $_; [Math]::Round((Get-IskDistance $p[0] $p[1]), 2) } | Sort-Object }

$crank = $addin.Insert('kin.link')
$S.Slide.Shapes.Item($crank).Select(); Start-Sleep 3
$addin.Command('port:joint0:kin.ground') | Out-Null; Start-Sleep 4
# The pivot sits exactly on the crank's joint 0.
$pivot = @(Get-IskShapes $S 'ISK:kin.joint.ground*')[0]
$m = Get-IskModel $pivot
Write-IskInfo ('pivot ' + $m.Substring($m.IndexOf('"transform"'), 60))
$cs = @(Get-IskShapes $S 'ISK:kin.link*')[0]
$crankName = $cs.Name
$cs.Select(); Start-Sleep 3
$addin.Command('port:joint1:kin.link') | Out-Null; Start-Sleep 4  # coupler
$addin.Command('port:joint1:kin.link') | Out-Null; Start-Sleep 4  # rocker
$addin.Command('port:joint1:kin.ground') | Out-Null; Start-Sleep 4 # second pivot
Write-IskShapes $S
Assert-Isk $S (@(Get-IskShapes $S 'ISK:kin.link*').Count -eq 3 -and @(Get-IskShapes $S 'ISK:kin.joint.ground*').Count -eq 2) 'three links and two fixed pivots built with the "+"'

$cs = $S.Slide.Shapes.Item($crankName)
$cs.Select(); Start-Sleep 3
Save-IskShot $S '1-built'
$lengths = @(Lengths)
$p = Get-IskPoints $cs
Assert-Isk $S ($addin.Command('motion')) 'motion mode switches on'
Start-Sleep 2
$cs.Select(); Start-Sleep 3
$pv = ConvertTo-IskPixel $S $p[0]; $tp = ConvertTo-IskPixel $S $p[1]
Invoke-IskArcDrag -Center $pv -Radius (Get-IskDistance $pv $tp) -Start ([Math]::Atan2($tp[1] - $pv[1], $tp[0] - $pv[0])) -Sweep 0.9 -BeforeRelease { Save-IskShot $S '2-dragging' }
Start-Sleep 2
$moved = @(Lengths)
Write-IskInfo ('link lengths {0} -> {1}' -f ($lengths -join ', '), ($moved -join ', '))
Assert-Isk $S (($lengths -join ',') -eq ($moved -join ',')) 'dragging keeps every link length'
$tip = (Get-IskPoints @(Get-IskShapes $S 'ISK:kin.link*' | Where-Object { (Get-IskDistance (Get-IskPoints $_)[0] $p[0]) -lt 0.01 } | Select-Object -First 1))[1]
Assert-Isk $S ((Get-IskDistance $tip $p[1]) -gt 1) ('the crank turned (tip moved {0:0.0} mm)' -f (Get-IskDistance $tip $p[1]))
$addin.Command('motion') | Out-Null
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '3-after'
