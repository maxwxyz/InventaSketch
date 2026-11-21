# Scenario: an e-machine gets a shaft through its hub from the bar and takes the shaft along when moved.
param($S)
$addin = $S.AddIn

$em = $addin.Insert('machines.emachine.stick')
$e = $S.Slide.Shapes.Item($em); $e.Left = 120; $e.Top = 300
$e.Select(); Start-Sleep 3
$addin.Command('ins:mech.shaft.line') | Out-Null; Start-Sleep 4
Write-IskShapes $S
$shafts = @(Get-IskShapes $S 'ISK:mech.shaft*')
Assert-Isk $S ($shafts.Count -ge 1) 'the bar inserts a shaft for the e-machine'
if (-not $shafts.Count) { return }
$before = $shafts[-1].Top
$emShape = @(Get-IskShapes $S 'ISK:machines*')[0]
$emShape.Select(); Start-Sleep 3
$emShape.Top = $emShape.Top + 40; Start-Sleep 5
$after = @(Get-IskShapes $S 'ISK:mech.shaft*')[-1].Top
Assert-Isk $S ([Math]::Abs($after - $before - 40) -lt 1) ('moving the e-machine 40 pt down moves its shaft along ({0:0.0} -> {1:0.0})' -f $before, $after)
Save-IskShot $S '1-motor-moved'
