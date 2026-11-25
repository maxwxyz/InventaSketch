# Scenario: a chart (efficiency map, line chart) is clicked with the real mouse, then dragged: it is selected after the
# click and moves to the drop point (maintainer report 2026-11-25: a click on an efficiency map started a move that could
# not be placed any more).
param($S)
$addin = $S.AddIn

foreach ($item in 'chart.efficiency', 'chart.line') {
    $S.Window.Selection.Unselect()
    $name = $addin.Insert($item); $shape = $S.Slide.Shapes.Item($name)
    $shape.Left = 120; $shape.Top = 90
    $S.Window.Selection.Unselect(); Start-Sleep 2
    $r = Get-IskRect $S $name
    Write-IskInfo ("{0}: {1} at {2},{3} {4}x{5} px" -f $item, $name, $r.X, $r.Y, $r.W, $r.H)
    $left0 = $shape.Left; $top0 = $shape.Top
    Invoke-IskClick ($r.X + $r.W * 0.45) ($r.Y + $r.H * 0.45) 2500
    $sel = try { $S.Window.Selection.ShapeRange.Item(1).Name } catch { '' }
    Save-IskShot $S "$item-1-clicked"
    Assert-Isk $S ($sel -eq $name) "$item is selected by a click ($sel)"
    $r = Get-IskRect $S $name
    $from = @(($r.X + $r.W * 0.45), ($r.Y + $r.H * 0.45)); $to = @(($from[0] + 120), ($from[1] + 60))
    Invoke-IskDrag -From $from -To $to -WaitMs 3000
    Save-IskShot $S "$item-2-dragged"
    $shape = $S.Slide.Shapes.Item($name)
    $dx = $shape.Left - $left0; $dy = $shape.Top - $top0
    Write-IskInfo ("{0} moved by {1:N1} / {2:N1} pt" -f $item, $dx, $dy)
    Assert-Isk $S ([math]::Abs($dx) -gt 20 -and [math]::Abs($dy) -gt 10) "$item moves with a drag and is dropped"
    # A click on the empty slide afterwards must not place anything (no move or insert is pending).
    $count = $S.Slide.Shapes.Count
    $pt = ConvertTo-IskPixel $S @(260, 20)
    Invoke-IskClick $pt[0] $pt[1] 2000
    Assert-Isk $S ($S.Slide.Shapes.Count -eq $count -and [math]::Abs($S.Slide.Shapes.Item($name).Left - $shape.Left) -lt 1) "${item}: nothing pending after the drop"
    Send-IskEscape; Start-Sleep 1
}
Save-IskShot $S '3-end'
