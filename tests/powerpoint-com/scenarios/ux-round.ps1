# Scenario: UI/UX round – context-only transforms, multi-selection bar, assembly view switch, insert box (Ctrl+Space).
param($S)
$addin = $S.AddIn

# 1) Context-only transforms: an axial gear (symmetric) offers no mirror/rotate, an arrow offers mirror horizontally.
$g = $addin.Insert('mech.spur.axial')
$gs = $S.Slide.Shapes.Item($g); $gs.Left = 80; $gs.Top = 80
$gs.Select(); Start-Sleep 3
$ids = Get-IskBarIds $S
Write-IskInfo ('gear bar: ' + ($ids -join ' '))
Assert-Isk $S (($ids -notcontains 'flipH') -and ($ids -notcontains 'flipV') -and ($ids -notcontains 'rot90') -and ($ids -contains 'insert')) 'an axial gear shows no mirror/rotate, but the insert button'
Save-IskShot $S '1-gear-bar'
$S.Window.Selection.Unselect()
$a = $addin.Insert('ann.arrow')
$as = $S.Slide.Shapes.Item($a); $as.Left = 420; $as.Top = 80
$as.Select(); Start-Sleep 3
$ids = Get-IskBarIds $S
Write-IskInfo ('arrow bar: ' + ($ids -join ' '))
Assert-Isk $S ($ids -contains 'flipH') 'an arrow can be mirrored horizontally'
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }

# 2) Multi-selection: two gears, centres on one row.
$g1 = $addin.Insert('mech.spur.axial'); $s1 = $S.Slide.Shapes.Item($g1); $s1.Left = 100; $s1.Top = 100
$g2 = $addin.Insert('mech.spur.axial'); $s2 = $S.Slide.Shapes.Item($g2); $s2.Left = 400; $s2.Top = 180
$S.Slide.Shapes.Range([string[]]@($g1, $g2)).Select(); Start-Sleep 3
$ids = Get-IskBarIds $S
Write-IskInfo ('multi bar: ' + ($ids -join ' '))
Assert-Isk $S ($ids -contains 'align:row') 'two misaligned gears offer "centres on one row"'
Save-IskShot $S '2-multi-bar'
Assert-Isk $S ($addin.Command('align:row')) 'the command runs on the multi-selection'
Start-Sleep 5
$c = @(Get-IskShapes $S 'ISK:mech.gear.spur*' | ForEach-Object { $_.Top + $_.Height / 2 })
Assert-Isk $S ($c.Count -eq 2 -and [Math]::Abs($c[0] - $c[1]) -lt 0.5) ('centres on one row ({0})' -f ($c -join ' / '))
$S.Window.Selection.Unselect()
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }

# 3) Assembly: a gear with a meshing partner from its build port, select the assembly, switch it to the stick view.
$g1 = $addin.Insert('mech.spur.axial'); $s1 = $S.Slide.Shapes.Item($g1); $s1.Left = 150; $s1.Top = 150
$s1.Select(); Start-Sleep 3
$addin.Command('port:mesh.right:mech.spur.axial') | Out-Null; Start-Sleep 5
@(Get-IskShapes $S 'ISK:mech.gear.spur*')[0].Select(); Start-Sleep 3
$ids = Get-IskBarIds $S
Assert-Isk $S ($ids -contains 'assembly') 'a coupled gear offers "select assembly"'
$addin.Command('assembly') | Out-Null; Start-Sleep 4
$ids = Get-IskBarIds $S
Write-IskInfo ('assembly bar: ' + ($ids -join ' '))
Assert-Isk $S ($S.Window.Selection.ShapeRange.Count -eq 2 -and ($ids -contains 'mview:stick')) 'the assembly is selected and offers its views'
$addin.Command('mview:stick') | Out-Null; Start-Sleep 6
$gears = @(Get-IskShapes $S 'ISK:mech.gear.spur*')
$views = @($gears | ForEach-Object { if ((Get-IskModel $_) -match '"view":\s*"([a-z]+)"') { $Matches[1] } })
$xs = @($gears | ForEach-Object { $_.Left + $_.Width / 2 })
Write-IskInfo ('views: ' + ($views -join ', ') + '; centres x: ' + ($xs -join ' / '))
Assert-Isk $S ($views.Count -eq 2 -and ($views | Where-Object { $_ -ne 'stick' }).Count -eq 0) 'both gears are in stick view'
Assert-Isk $S ($xs.Count -eq 2 -and [Math]::Abs($xs[0] - $xs[1]) -lt 0.5) 'meshing gears lie in one plane (same place along the axis)'
Save-IskShot $S '3-assembly-stick'
$S.Window.Selection.Unselect()
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }

# 4) Insert box: select a shaft, Ctrl+Space, type, Enter: the chosen part is placed (on the shaft or at the pointer).
$sh = $addin.Insert('mech.shaft.line'); $S.Slide.Shapes.Item($sh).Select(); Start-Sleep 3
$r = Get-IskRect $S $sh
Move-IskMouse ($r.CX) ($r.CY - 120); Start-Sleep -Milliseconds 400
Send-IskCtrlKey 0x20; Start-Sleep 2
Save-IskShot $S '4-insert-box' -Around ($r.CX - 100), ($r.CY - 200), 900, 700
[System.Windows.Forms.SendKeys]::SendWait('lager'); Start-Sleep 1
Save-IskShot $S '5-insert-typed' -Around ($r.CX - 100), ($r.CY - 200), 900, 700
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}'); Start-Sleep 3
# Placed where it fits, else it follows the pointer: a click places it.
if (@(Get-IskShapes $S 'ISK:mech.bearing*').Count -eq 0) { Invoke-IskClick ($r.CX) ($r.CY) -WaitMs 4000 }
Write-IskShapes $S
Assert-Isk $S (@(Get-IskShapes $S 'ISK:mech.bearing*').Count -ge 1) 'Ctrl+Space, "lager", Enter inserts a bearing'
Save-IskShot $S '6-inserted'
