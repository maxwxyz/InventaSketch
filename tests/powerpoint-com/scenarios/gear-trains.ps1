# Scenario: gear trains: a planetary set exploded into parts, a stage built on a carrier, a bevel pair.
param($S)
$addin = $S.AddIn
function Count([string]$pattern) { @(Get-IskShapes $S $pattern).Count }

$set = $addin.Insert('mech.planetary.teeth')
$set = $S.Slide.Shapes.Item($set); $set.Left = 120; $set.Top = 120
$set.Select(); Start-Sleep 3
$addin.Command('act:param.planets.inc') | Out-Null; Start-Sleep 3
(Get-IskSelected $S).Select(); Start-Sleep 2
Assert-Isk $S ($addin.Command('explode')) 'a planetary set explodes'
Start-Sleep 6
Assert-Isk $S ((Count 'ISK:mech.gear.internal*') -eq 1 -and (Count 'ISK:mech.carrier*') -eq 1 -and (Count 'ISK:mech.gear.spur*') -ge 5) ('parts: {0} spur, {1} ring, {2} carrier' -f (Count 'ISK:mech.gear.spur*'), (Count 'ISK:mech.gear.internal*'), (Count 'ISK:mech.carrier*'))
Save-IskShot $S '1-exploded'

$S.Window.Selection.Unselect()
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
$c = $addin.Insert('mech.carrier.axial')
$cs = $S.Slide.Shapes.Item($c); $cs.Left = 360; $cs.Top = 150
$cs.Select(); Start-Sleep 3
$addin.Command('port:hub:mech.spur.axial') | Out-Null; Start-Sleep 4
$cs = @(Get-IskShapes $S 'ISK:mech.carrier*')[-1]
foreach ($pin in 'pin0', 'pin1', 'pin2') { $cs.Select(); Start-Sleep 2; $addin.Command("port:${pin}:mech.spur.axial") | Out-Null; Start-Sleep 4 }
$cs.Select(); Start-Sleep 2
$addin.Command('port:hub:mech.ring.axial') | Out-Null; Start-Sleep 4
Assert-Isk $S ((Count 'ISK:mech.gear.spur*') -eq 4 -and (Count 'ISK:mech.gear.internal*') -eq 1) ('stage from a carrier: {0} spur, {1} ring' -f (Count 'ISK:mech.gear.spur*'), (Count 'ISK:mech.gear.internal*'))
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-built-stage'

foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
$b = $addin.Insert('mech.bevel.stick')
$bs = $S.Slide.Shapes.Item($b); $bs.Left = 300; $bs.Top = 260
$bs.Select(); Start-Sleep 3
$addin.Command('port:bevel.up:mech.bevel.stick') | Out-Null; Start-Sleep 4
Assert-Isk $S ((Count 'ISK:mech.gear.bevel*') -eq 2) 'a bevel gear docks on the bevel port'
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '3-bevel'
