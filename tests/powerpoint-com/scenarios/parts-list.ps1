# Scenario: a parts list table counts the gears on the slide and follows when another gear is inserted.
param($S)
$addin = $S.AddIn

function Texts($shape) {
    $out = @()
    if ($shape.Type -eq 6) { foreach ($i in @($shape.GroupItems)) { if ($i.HasTextFrame -and $i.TextFrame.HasText) { $out += $i.TextFrame.TextRange.Text } } }
    elseif ($shape.HasTextFrame -and $shape.TextFrame.HasText) { $out += $shape.TextFrame.TextRange.Text }
    , $out
}

$g1 = $addin.Insert('mech.spur.axial'); $s1 = $S.Slide.Shapes.Item($g1); $s1.Left = 60; $s1.Top = 60
$g2 = $addin.Insert('mech.spur.axial'); $s2 = $S.Slide.Shapes.Item($g2); $s2.Left = 260; $s2.Top = 60
$S.Window.Selection.Unselect()
$t = $addin.Insert('ann.partslist'); $ts = $S.Slide.Shapes.Item($t); $ts.Left = 120; $ts.Top = 300; $ts.Width = 300; $ts.Height = 90
Start-Sleep 4
$table = @(Get-IskShapes $S 'ISK:ann.partslist*')[0]
$texts = Texts $table
Write-IskInfo ('table: ' + ($texts -join ' | '))
Assert-Isk $S ($texts -contains '2') 'the parts list counts two gears'
$S.Window.Selection.Unselect()
$g3 = $addin.Insert('mech.spur.axial'); $s3 = $S.Slide.Shapes.Item($g3); $s3.Left = 460; $s3.Top = 60
Start-Sleep 5
$table = @(Get-IskShapes $S 'ISK:ann.partslist*')[0]
$texts = Texts $table
Write-IskInfo ('table after: ' + ($texts -join ' | '))
Assert-Isk $S ($texts -contains '3') 'the parts list follows a newly inserted gear'
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '1-parts-list'

# Two gears selected: a dimension between their axes, docked on both.
@(Get-IskShapes $S 'ISK:mech.gear.spur*')[0].Select(); Start-Sleep 2
@(Get-IskShapes $S 'ISK:mech.gear.spur*')[1].Select(0); Start-Sleep 3
$ids = Get-IskBarIds $S
Write-IskInfo ('multi bar: ' + ($ids -join ' '))
Assert-Isk $S ($ids -contains 'dim') 'two selected parts offer a dimension between them'
$addin.Command('dim') | Out-Null; Start-Sleep 5
Assert-Isk $S (@(Get-IskShapes $S 'ISK:ann.dimension*').Count -eq 1) 'the dimension is inserted'
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-dimension'
