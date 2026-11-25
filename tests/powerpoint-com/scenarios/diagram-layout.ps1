# Scenario: a connected flow (three steps chained with the "+" below each step) is scattered and laid out again with the
# multi-selection command "layout:down" (DiagramLayout): the steps stand in one column, top to bottom, connectors docked.
param($S)
$addin = $S.AddIn
$k = Get-IskScale

$name = $addin.Insert('ann.flow.process')
$S.Slide.Shapes.Item($name).Left = 300; $S.Slide.Shapes.Item($name).Top = 60
for ($i = 0; $i -lt 2; $i++) {
    $steps = @(Get-IskShapes $S 'ISK:ann.flow.process*' | Sort-Object Top)
    $last = $steps[$steps.Count - 1]
    $S.Window.Selection.Unselect(); Start-Sleep 1
    $last.Select(); Start-Sleep 2
    $r = Get-IskRect $S $last.Name
    Invoke-IskClick $r.CX ($r.Y + $r.H + 13 * $k) -WaitMs 3000
}
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*')
Assert-Isk $S ($steps.Count -eq 3) "three chained steps ($($steps.Count))"
# Scatter them.
$S.Window.Selection.Unselect(); Start-Sleep 1
$offsets = @(@(-200, 40), @(180, -30), @(-60, 120))
for ($i = 0; $i -lt $steps.Count; $i++) { $steps[$i].Left += $offsets[$i][0]; $steps[$i].Top += $offsets[$i][1]; Start-Sleep 1 }
Start-Sleep 2
Save-IskShot $S '1-scattered'
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*')
$S.Slide.Shapes.Range([string[]]@($steps | ForEach-Object { $_.Name })).Select(); Start-Sleep 3
$ids = Wait-IskBarIds $S { param($x) $x -contains 'layout:down' }
Write-IskInfo ('multi bar: ' + ($ids -join ' '))
Assert-Isk $S ($ids -contains 'layout:down') 'the multi-selection offers the diagram layout'
Assert-Isk $S ($addin.Command('layout:down')) 'the layout runs'
Start-Sleep 5
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*' | Sort-Object Top)
$cx = @($steps | ForEach-Object { $_.Left + $_.Width / 2 })
Write-IskInfo ('centres x: ' + (($cx | ForEach-Object { '{0:0.0}' -f $_ }) -join ' / '))
Assert-Isk $S (($cx | Measure-Object -Maximum -Minimum | ForEach-Object { $_.Maximum - $_.Minimum }) -lt 1) 'the steps stand in one column'
# The connectors follow: each arrow runs from the bottom of one step to the top of the next.
$arrows = @(Get-IskShapes $S 'ISK:ann.arrow*' | Sort-Object Top)
Assert-Isk $S ($arrows.Count -eq 2) "two connectors ($($arrows.Count))"
for ($i = 0; $i -lt [Math]::Min($arrows.Count, $steps.Count - 1); $i++) {
    $gapTop = [Math]::Abs($arrows[$i].Top - ($steps[$i].Top + $steps[$i].Height))
    $gapBottom = [Math]::Abs(($arrows[$i].Top + $arrows[$i].Height) - $steps[$i + 1].Top)
    Write-IskInfo ("arrow {0}: {1:0.0} pt from the step above, {2:0.0} pt from the step below" -f $i, $gapTop, $gapBottom)
    Assert-Isk $S ($gapTop -lt 6 -and $gapBottom -lt 6) "arrow $i joins the steps"
}
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-laid-out'

# Align on the connected steps: lay them out in a row, shift the middle one down, then "centres on one horizontal line" –
# the arrows must follow (align is a model edit that redraws the docked connectors).
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*' | Sort-Object Top)
$S.Window.Selection.Unselect(); Start-Sleep 1
$S.Slide.Shapes.Range([string[]]@($steps | ForEach-Object { $_.Name })).Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('layout:right')) 'the left-to-right layout runs'
Start-Sleep 5
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*' | Sort-Object Left)
$S.Window.Selection.Unselect(); Start-Sleep 1
$steps[1].Top += 60; Start-Sleep 1
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*' | Sort-Object Left)
$S.Slide.Shapes.Range([string[]]@($steps | ForEach-Object { $_.Name })).Select(); Start-Sleep 3
$ids = Wait-IskBarIds $S { param($x) $x -contains 'align:row' }
Assert-Isk $S ($addin.Command('align:row')) 'centres on one horizontal line'
Start-Sleep 5
$steps = @(Get-IskShapes $S 'ISK:ann.flow.process*' | Sort-Object Left)
$cy = @($steps | ForEach-Object { $_.Top + $_.Height / 2 })
Assert-Isk $S (($cy | Measure-Object -Maximum -Minimum | ForEach-Object { $_.Maximum - $_.Minimum }) -lt 1) 'the steps stand in one row'
$arrows = @(Get-IskShapes $S 'ISK:ann.arrow*' | Sort-Object Left)
for ($i = 0; $i -lt [Math]::Min($arrows.Count, $steps.Count - 1); $i++) {
    $a = $arrows[$i]
    $gapLeft = [Math]::Abs($a.Left - ($steps[$i].Left + $steps[$i].Width))
    $gapRight = [Math]::Abs(($a.Left + $a.Width) - $steps[$i + 1].Left)
    Write-IskInfo ("arrow {0} after align: {1:0.0} pt from the left step, {2:0.0} pt from the right step" -f $i, $gapLeft, $gapRight)
    Assert-Isk $S ($gapLeft -lt 6 -and $gapRight -lt 6) "arrow $i still joins its steps after aligning"
}
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '3-aligned'
