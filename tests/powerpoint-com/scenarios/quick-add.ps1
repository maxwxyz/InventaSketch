# Scenario: quick add below a flow step ("+" triangle) and connecting two ER entities by dragging a "+" onto the other.
param($S)
$addin = $S.AddIn
$k = Get-IskScale

# A flow step: clicking the triangle below its frame adds a connected next step.
$name = $addin.Insert('ann.flow.process')
$S.Slide.Shapes.Item($name).Select(); Start-Sleep 2
$r = Get-IskRect $S $name
Save-IskShot $S '1-flow-selected' -Around ($r.X - 120), ($r.Y - 120), ($r.W + 240), ($r.H + 360)
$before = $S.Slide.Shapes.Count
Invoke-IskClick $r.CX ($r.Y + $r.H + 13 * $k) -WaitMs 3000
Write-IskShapes $S
Assert-Isk $S ($S.Slide.Shapes.Count -gt $before) "the '+' below a flow step adds a step ($before -> $($S.Slide.Shapes.Count) shapes)"
Save-IskShot $S '2-flow-added' -Around ($r.X - 120), ($r.Y - 120), ($r.W + 240), ($r.H * 3 + 360)

# Two ER entities: drag from the right triangle of A onto the left edge of B.
$S.Window.Selection.Unselect()
foreach ($shape in @($S.Slide.Shapes)) { $shape.Delete() }
$a = $addin.Insert('diag.er.entity')
$b = $addin.Insert('diag.er.entity.weak')
$S.Slide.Shapes.Item($a).Left -= 180
$S.Slide.Shapes.Item($b).Left += 120
$S.Slide.Shapes.Item($b).Top += 60
$S.Slide.Shapes.Item($a).Select(); Start-Sleep 2
$ra = Get-IskRect $S $a; $rb = Get-IskRect $S $b
$before = $S.Slide.Shapes.Count
Invoke-IskDrag -From @(($ra.X + $ra.W + 13 * $k), $ra.CY) -To @(($rb.X + 2), $rb.CY)
Write-IskShapes $S
Assert-Isk $S ($S.Slide.Shapes.Count -gt $before) "dragging the '+' of an entity onto another adds a connector ($before -> $($S.Slide.Shapes.Count) shapes)"
Save-IskShot $S '3-er-connected' -Around ($ra.X - 150), ($ra.Y - 200), ($rb.X + $rb.W - $ra.X + 300), ($rb.Y + $rb.H - $ra.Y + 400)
