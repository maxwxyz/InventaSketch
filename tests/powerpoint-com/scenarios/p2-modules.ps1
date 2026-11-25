# Scenario: one element of each P2 module (process & thermal, energy systems, robotics) is inserted as native shapes.
param($S)
$addin = $S.AddIn

$items = @(
    @{ Id = 'process.column'; Type = 'process.column' },
    @{ Id = 'process.pump.centrifugal'; Type = 'process.pump.centrifugal' },
    @{ Id = 'energy.pv.array'; Type = 'energy.pv.array' },
    @{ Id = 'energy.grid.transformer.sld'; Type = 'energy.grid.transformer' },
    @{ Id = 'robot.arm.articulated'; Type = 'robot.arm.articulated' },
    @{ Id = 'robot.gripper.parallel'; Type = 'robot.gripper.parallel' }
)
$x = 30
foreach ($it in $items) {
    $S.Window.Selection.Unselect()
    $name = $addin.Insert($it.Id)
    Assert-Isk $S ($name -ne '') ("inserted " + $it.Id)
    if ($name -eq '') { continue }
    $shape = $S.Slide.Shapes.Item($name)
    $shape.Left = $x; $shape.Top = 120
    $x += [Math]::Max(90, $shape.Width + 20)
    $parts = if ($shape.Type -eq 6) { $shape.GroupItems.Count } else { 1 }
    Write-IskInfo ("{0}: {1} native shape(s), {2:0} x {3:0} pt" -f $it.Id, $parts, $shape.Width, $shape.Height)
    Assert-Isk $S ($shape.Name -like ('ISK:' + $it.Type + '*')) ($it.Id + ' carries its type in the shape name')
    Assert-Isk $S ($shape.Width -gt 5 -and $shape.Height -gt 5) ($it.Id + ' has a visible size')
}
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '1-p2-modules'
