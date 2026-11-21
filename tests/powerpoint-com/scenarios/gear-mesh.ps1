# Scenario: two gears snap into mesh when dragged together; gears inserted on a shaft and meshing from the bar.
param($S)
$addin = $S.AddIn

$a = $addin.Insert('mech.spur.axial')
$b = $addin.Insert('mech.spur.axial')
$S.Slide.Shapes.Item($b).Select(); Start-Sleep 2
$rb = Get-IskRect $S $b
$before = @(Get-IskShapes $S | ForEach-Object { '{0:0.0},{1:0.0}' -f $_.Left, $_.Top })
Invoke-IskDrag -From @($rb.CX, $rb.CY) -To @(($rb.CX + $rb.W * 0.9), $rb.CY)
$after = @(Get-IskShapes $S | ForEach-Object { '{0:0.0},{1:0.0}' -f $_.Left, $_.Top })
Write-IskShapes $S
Assert-Isk $S (($after -join '|') -ne ($before -join '|')) 'dragging a gear next to another moves it (check the shot: teeth in mesh)'
Save-IskShot $S '1-meshed' -Around ($rb.X - $rb.W * 2), ($rb.Y - $rb.H), ($rb.W * 5), ($rb.H * 3)

# A shaft: "insert gear" puts a gear on it, a second one meshes with the first.
$S.Window.Selection.Unselect()
foreach ($shape in @($S.Slide.Shapes)) { $shape.Delete() }
$shaft = $addin.Insert('mech.shaft.line')
$S.Slide.Shapes.Item($shaft).Select(); Start-Sleep 2
$addin.Command('ins:mech.spur.stick') | Out-Null; Start-Sleep 4
$addin.Command('ins:mech.spur.stick') | Out-Null; Start-Sleep 4
Write-IskShapes $S
Assert-Isk $S (@(Get-IskShapes $S 'ISK:mech.gear.spur*').Count -eq 2) 'two gears were inserted from the bar'
Save-IskShot $S '2-shaft-gears'
