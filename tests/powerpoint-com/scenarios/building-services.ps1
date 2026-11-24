# Scenario: a small bathroom installation (washbasin, WC, socket) switches from plan to section and back as one installation.
param($S)
$addin = $S.AddIn

$a = $addin.Insert('bs.san.basin'); $sa = $S.Slide.Shapes.Item($a); $sa.Left = 150; $sa.Top = 150
$b = $addin.Insert('bs.san.wc'); $sb = $S.Slide.Shapes.Item($b); $sb.Left = 300; $sb.Top = 150
$c = $addin.Insert('elec.house.socket'); $sc = $S.Slide.Shapes.Item($c); $sc.Left = 450; $sc.Top = 150
function Parts { @(Get-IskShapes $S 'ISK:bs.*') + @(Get-IskShapes $S 'ISK:elec.house*') }
function SelectAll { $all = Parts; $all[0].Select(); Start-Sleep 2; for ($i = 1; $i -lt $all.Count; $i++) { $all[$i].Select(0); Start-Sleep 1 }; Start-Sleep 2 }
$xBefore = @(Parts | Sort-Object Left | ForEach-Object { $_.Left + $_.Width / 2 })
SelectAll
$ids = Get-IskBarIds $S
Write-IskInfo ('multi bar: ' + ($ids -join ' '))
Assert-Isk $S (($ids -contains 'mview:section') -and ($ids -contains 'mview:schematic')) 'the installation offers section and schematic'
$addin.Command('mview:section') | Out-Null; Start-Sleep 6
$views = @(Parts | ForEach-Object { if ((Get-IskModel $_) -match '"view":\s*"([a-z]+)"') { $Matches[1] } })
Assert-Isk $S ($views.Count -eq 3 -and ($views | Where-Object { $_ -ne 'section' }).Count -eq 0) ('all three are in section view (' + ($views -join ', ') + ')')
$xSection = @(Parts | Sort-Object Left | ForEach-Object { $_.Left + $_.Width / 2 })
Assert-Isk $S ((0..2 | Where-Object { [Math]::Abs($xSection[$_] - $xBefore[$_]) -ge 1 }).Count -eq 0) 'the x positions stay'
Save-IskShot $S '1-section'
SelectAll
$addin.Command('mview:plan') | Out-Null; Start-Sleep 6
$tops = @(Parts | ForEach-Object { $_.Top })
Assert-Isk $S ($tops.Count -eq 3 -and ($tops | Where-Object { [Math]::Abs($_ - 150) -gt 1 }).Count -eq 0) ('the plan comes back (' + ($tops -join ', ') + ')')
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-plan-again'
