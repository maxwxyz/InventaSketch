# Scenario: a gripper built onto a robot flange with "+" faces along the tool axis (directed mounts).
param($S)
$addin = $S.AddIn

$r = $addin.Insert('robot.arm.articulated')
$rs = $S.Slide.Shapes.Item($r); $rs.Left = 200; $rs.Top = 80
$S.Window.Selection.Unselect(); Start-Sleep 1
$rs.Select(); Start-Sleep 3
$ids = Wait-IskBarIds $S { param($i) $i.Count -gt 0 }
Write-IskInfo ('robot bar: ' + ($ids -join ' '))
$before = @(Get-IskShapes $S 'ISK:robot.gripper*').Count
Assert-Isk $S ($addin.Command('port:mount.flange:robot.gripper.parallel')) 'the flange port takes a parallel gripper'
Start-Sleep 4
$grippers = @(Get-IskShapes $S 'ISK:robot.gripper*')
Assert-Isk $S ($grippers.Count -eq $before + 1) 'a gripper is on the slide'
if ($grippers.Count -gt 0) {
    $model = Get-IskModel $grippers[0]
    $rot = if ($model -match '"rotation":\s*(-?[0-9.]+)') { [double]$Matches[1] } else { 0 }
    Write-IskInfo ("gripper rotation in the model: {0:0.0}°" -f $rot)
}
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '1-robot-gripper'
