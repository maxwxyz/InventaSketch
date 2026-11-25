# Scenario (maintainer 2026-11-25): "Clean slides" normalises the chart values of the slide (same shape, 0..1) and the
# report names what changed; randomised with a seed gives different values.
param($S)
$addin = $S.AddIn

$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 120; $shape.Top = 90
$S.Window.Selection.Unselect(); Start-Sleep 1
$before = Get-IskModel $S.Slide.Shapes.Item($name)
Assert-Isk $S ($before -match '0;1.2') 'the chart has its sample data'
$report = $addin.CleanSlides('slide', 'normalised', 7)
Start-Sleep 2
Write-IskInfo ("report: " + ($report -split "`n")[0])
$after = (@(Get-IskShapes $S) | ForEach-Object { Get-IskModel $_ }) -join ' '
Assert-Isk $S ($after -notmatch '0;1.2' -and $after -match 'chart\.xy') 'the values are no longer the original ones'
Assert-Isk $S ($report -match '1') 'the report counts the cleaned element'
Save-IskShot $S '1-cleaned'
