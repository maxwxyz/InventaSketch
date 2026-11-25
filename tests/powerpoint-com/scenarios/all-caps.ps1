# Scenario (maintainer 2026-11-25): the "All caps" toggle right of superscript. An InventaSketch label shows its text in
# capitals while the model text keeps its case; switching off restores it. A native text box gets PowerPoint's own
# all-caps attribute.
param($S)
$addin = $S.AddIn

function Get-UnitText($shape) {
    $texts = @()
    if ($shape.Type -eq 6) { foreach ($c in $shape.GroupItems) { if ($c.HasTextFrame -and $c.TextFrame.HasText) { $texts += $c.TextFrame.TextRange.Text } } }
    elseif ($shape.HasTextFrame -and $shape.TextFrame.HasText) { $texts += $shape.TextFrame.TextRange.Text }
    $texts -join ' '
}

$S.Window.Selection.Unselect()
$name = $addin.Insert('ann.text'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 120; $shape.Top = 120
$before = Get-UnitText $shape
Write-IskInfo ("text before: '$before'")
$shape.Select(); Start-Sleep 1
$pressed = $addin.SetAllCaps($true); Start-Sleep 2
$unit = @(Get-IskShapes $S) | Select-Object -First 1
$after = Get-UnitText $unit
Write-IskInfo ("text with caps: '$after'")
Assert-Isk $S ($pressed) 'the toggle reads back as pressed'
Assert-Isk $S ($after -ceq $after.ToUpperInvariant() -and $after -ne '') 'the element shows capitals'
Assert-Isk $S ((Get-IskModel $unit) -match [regex]::Escape($before)) 'the model text keeps its case'
Save-IskShot $S '1-caps'
$unit.Select(); Start-Sleep 1
$addin.SetAllCaps($false) | Out-Null; Start-Sleep 2
$unit = @(Get-IskShapes $S) | Select-Object -First 1
Assert-Isk $S ((Get-UnitText $unit) -ceq $before) 'switching off restores the case'

# Native text box.
$S.Window.Selection.Unselect()
$box = $S.Slide.Shapes.AddTextbox(1, 400, 300, 200, 40); $box.TextFrame.TextRange.Text = 'Native text'
$box.Select(); Start-Sleep 1
$addin.SetAllCaps($true) | Out-Null; Start-Sleep 1
Assert-Isk $S ($box.TextFrame2.TextRange.Font.Allcaps -eq -1) 'a native text box gets PowerPoint all caps'
Send-IskEscape; Start-Sleep 1
