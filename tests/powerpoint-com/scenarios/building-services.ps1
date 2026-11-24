# Scenario: a small bathroom installation (washbasin, WC) switches from plan to section and back with the mechanism kept.
param($S)
$addin = $S.AddIn

$a = $addin.Insert('bs.san.basin'); $sa = $S.Slide.Shapes.Item($a); $sa.Left = 150; $sa.Top = 150
$b = $addin.Insert('bs.san.wc'); $sb = $S.Slide.Shapes.Item($b); $sb.Left = 300; $sb.Top = 150
$xBefore = @(@(Get-IskShapes $S 'ISK:bs.*') | Sort-Object Left | ForEach-Object { $_.Left + $_.Width / 2 })
@(Get-IskShapes $S 'ISK:bs.*')[0].Select(); Start-Sleep 2
@(Get-IskShapes $S 'ISK:bs.*')[1].Select(0); Start-Sleep 3
$ids = Get-IskBarIds $S
Write-IskInfo ('multi bar: ' + ($ids -join ' '))
Assert-Isk $S (($ids -contains 'mview:section') -and ($ids -contains 'mview:schematic')) 'the installation offers section and schematic'
$addin.Command('mview:section') | Out-Null; Start-Sleep 6
$views = @(Get-IskShapes $S 'ISK:bs.*' | ForEach-Object { if ((Get-IskModel $_) -match '"view":\s*"([a-z]+)"') { $Matches[1] } })
Assert-Isk $S ($views.Count -eq 2 -and ($views | Where-Object { $_ -ne 'section' }).Count -eq 0) ('both are in section view (' + ($views -join ', ') + ')')
$xSection = @(@(Get-IskShapes $S 'ISK:bs.*') | Sort-Object Left | ForEach-Object { $_.Left + $_.Width / 2 })
Assert-Isk $S ([Math]::Abs($xSection[0] - $xBefore[0]) -lt 1 -and [Math]::Abs($xSection[1] - $xBefore[1]) -lt 1) 'the x positions stay'
Save-IskShot $S '1-section'
@(Get-IskShapes $S 'ISK:bs.*')[0].Select(); Start-Sleep 2
@(Get-IskShapes $S 'ISK:bs.*')[1].Select(0); Start-Sleep 3
$addin.Command('mview:plan') | Out-Null; Start-Sleep 6
$tops = @(Get-IskShapes $S 'ISK:bs.*' | ForEach-Object { $_.Top })
Assert-Isk $S ($tops.Count -eq 2 -and ($tops | Where-Object { [Math]::Abs($_ - 150) -gt 1 }).Count -eq 0) ('the plan comes back (' + ($tops -join ', ') + ')')
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-plan-again'
