# Scenario (charts v2 stage 3): a curve sketched with the real mouse into a line chart is recognised and added as a new
# series (bar command "sketch", then draw with the left button held).
param($S)
$addin = $S.AddIn

$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 120; $shape.Top = 90; $shape.Width = 480; $shape.Height = 300
$shape.Select(); Start-Sleep 2
$before = (Get-IskModel $S.Slide.Shapes.Item($name))
Assert-Isk $S ($addin.BarIds() -match 'sketch') 'the chart bar offers "Sketch a curve"'
$addin.Command('sketch') | Out-Null; Start-Sleep 1

# A step drawn inside the plot area: low on the left, high on the right.
$r = Get-IskRect $S $name
$x0 = $r.X + $r.W * 0.25; $x1 = $r.X + $r.W * 0.85; $xm = ($x0 + $x1) / 2
$yLow = $r.Y + $r.H * 0.65; $yHigh = $r.Y + $r.H * 0.3
Move-IskMouse $x0 $yLow; Start-Sleep -Milliseconds 200
[IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
for ($i = 1; $i -le 20; $i++) { Move-IskMouse ($x0 + ($xm - $x0) * $i / 20) $yLow; Start-Sleep -Milliseconds 15 }
for ($i = 1; $i -le 8; $i++) { Move-IskMouse $xm ($yLow + ($yHigh - $yLow) * $i / 8); Start-Sleep -Milliseconds 15 }
for ($i = 1; $i -le 20; $i++) { Move-IskMouse ($xm + ($x1 - $xm) * $i / 20) $yHigh; Start-Sleep -Milliseconds 15 }
[IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep 3
Save-IskShot $S '1-sketched'
$unit = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'chart\.xy' } | Select-Object -First 1
$after = Get-IskModel $unit
$blocks = ([regex]::Matches(($after -replace '\\n', "`n"), "`n`n")).Count
Write-IskInfo ("data blocks after: $blocks")
Assert-Isk $S ($after -ne $before -and $after -match '\\n\\n') 'a new series was added to the chart data'
# The sketched curve remembers its trail: "Curve shape" switches the shape without losing it.
$unit.Select(); Start-Sleep 2
$addin.Command('partsel:series.1') | Out-Null; Start-Sleep 2
$bar = $addin.BarIds()
Write-IskInfo ("bar of the sketched curve: " + $bar)
Assert-Isk $S ($bar -match 'part:chart\.curve\.shape') 'the sketched curve offers "Curve shape"'
$m1 = Get-IskModel $unit
$addin.Command('part:chart.curve.shape') | Out-Null; Start-Sleep 3
$unit = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'chart\.xy' } | Select-Object -First 1
$m2 = Get-IskModel $unit
Assert-Isk $S ($m2 -ne $m1 -and $m2 -match 'sketch shape=') 'switching the shape changes the curve and keeps the sketch memo'
Save-IskShot $S '2-shape-switched'
Send-IskEscape; Start-Sleep 1
