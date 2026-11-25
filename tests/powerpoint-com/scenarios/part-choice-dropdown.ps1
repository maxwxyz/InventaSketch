# Scenario (maintainer 2026-11-25: "Curve shape is a switch to click through, not a dropdown"): a choice among several
# options on a part is a dropdown, never a cycle button. A curve sketched into an XY chart; its bar has the "Curve shape"
# dropdown (the current shape as a picture and a caret); a click opens the list of every shape with its picture, the
# current one marked; picking one switches the shape. In the pie, the Style place lists "Curve shape: <current> >" which
# opens the same options. No cycle action is left on the part.
param($S)
$addin = $S.AddIn

function Get-Chart { @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'chart\.xy' } | Select-Object -First 1 }

$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 160; $shape.Top = 100; $shape.Width = 480; $shape.Height = 300
$shape.Select(); Start-Sleep 2
$addin.Command('sketch') | Out-Null; Start-Sleep 1
# A wavy line drawn inside the plot area.
$r = Get-IskRect $S $name
$x0 = $r.X + $r.W * 0.2; $x1 = $r.X + $r.W * 0.85; $ym = $r.Y + $r.H * 0.45
Move-IskMouse $x0 $ym; Start-Sleep -Milliseconds 200
[IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
for ($i = 1; $i -le 40; $i++) { Move-IskMouse ($x0 + ($x1 - $x0) * $i / 40) ($ym + [math]::Sin($i / 5.0) * $r.H * 0.12); Start-Sleep -Milliseconds 15 }
[IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep 3
$chart = Get-Chart
Assert-Isk $S ((Get-IskModel $chart) -match 'sketch shape=') 'the sketched curve keeps its shape memo'
$chart.Select(); Start-Sleep 2
$addin.Command('partsel:series.1') | Out-Null; Start-Sleep 2
$r = Get-IskRect $S $chart.Name
$around = @(($r.X - 40), ($r.Y - 80), ($r.W + 80), ($r.H + 160))

# 1) The bar: a "Curve shape" dropdown button, no cycle command.
$bar = [string]$addin.BarIds()
Write-IskInfo ("bar: " + $bar)
Assert-Isk $S (($bar -split ',') -contains 'choice:@p.chart.curve.shape') 'the bar has the "Curve shape" dropdown'
$ids = [string]$addin.CommandIds()
Assert-Isk $S (-not (($ids -split ',') -contains 'part:chart.curve.shape')) 'there is no "next shape" cycle command any more'
Save-IskShot $S '1-bar' -Around $around

# 2) Open it: every shape with a picture, the current one marked.
$addin.Command('choice:@p.chart.curve.shape') | Out-Null; Start-Sleep 2
$rows = @(([string]$addin.DropItems()) -split "`n" | Where-Object { $_ })
Write-IskInfo ("dropdown: " + ($rows -join ' | '))
Assert-Isk $S ($rows.Count -ge 4) "the dropdown lists the curve shapes ($($rows.Count))"
Assert-Isk $S (@($rows | Where-Object { $_ -match '\(current\)' }).Count -eq 1) 'exactly one shape is marked current'
Assert-Isk $S (@($rows | Where-Object { $_ -match '\(picture\)' }).Count -eq $rows.Count) 'every shape option has a picture of the curve'
Assert-Isk $S (@($rows | Where-Object { $_ -match '^pick:@p\.chart\.curve\.shape:chart\.curve\.shape=raw' }).Count -eq 1) 'the list offers "As drawn"'
Save-IskShot $S '2-dropdown-open' -Around $around

# 3) Pick "As drawn": the shape changes, the memo stays.
$m1 = Get-IskModel (Get-Chart)
$addin.Command('pick:@p.chart.curve.shape:chart.curve.shape=raw') | Out-Null; Start-Sleep 3
$m2 = Get-IskModel (Get-Chart)
Assert-Isk $S ($m2 -ne $m1 -and $m2 -match 'sketch shape=raw') 'picking "As drawn" switches the curve shape'
Save-IskShot $S '3-picked' -Around $around

# 4) The pie: the Style place lists the choice as "Curve shape: As drawn >", opening the same options.
$chart = Get-Chart; $chart.Select(); Start-Sleep 2
$addin.Command('partsel:series.1') | Out-Null; Start-Sleep 2
Move-IskMouse ($r.X + $r.W / 2) ($r.Y + $r.H / 2); Start-Sleep -Milliseconds 300
$addin.Command('menu:g:style') | Out-Null; Start-Sleep 2
$pie = [string]$addin.PieItems()
Write-IskInfo ("pie style: " + ($pie -replace "`n", ' | '))
Assert-Isk $S ($pie -match 'menu:c:@p\.chart\.curve\.shape\t(Curve shape|Kurvenform): ') 'the Style place has "Curve shape: <current>"'
Save-IskShot $S '4-pie-style' -Around $around
$addin.Command('menu:c:@p.chart.curve.shape') | Out-Null; Start-Sleep 2
$pie2 = [string]$addin.PieItems()
Assert-Isk $S ($pie2 -match 'pick:@p\.chart\.curve\.shape:chart\.curve\.shape=smoothed') 'the pie submenu lists the shapes'
Save-IskShot $S '5-pie-options' -Around $around
Send-IskEscape; Start-Sleep 1
Send-IskEscape; Start-Sleep 1
