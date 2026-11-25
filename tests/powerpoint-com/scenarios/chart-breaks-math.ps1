# Scenario (charts v2 stages 3 and 4): an XY chart with an axis break draws the break marks as parts of their own;
# the coordinate system and the function graph (moved from the math module into the charts) insert natively and
# "Next function" redraws the graph.
param($S)
$addin = $S.AddIn

function Get-PartKeys($shape) {
    $keys = @()
    if ($shape.Type -eq 6) { foreach ($c in $shape.GroupItems) { $keys += $c.Tags.Item('ISK_PART') } }
    @($keys | Sort-Object -Unique)
}

# 1. Line chart with axis break.
$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line.break'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 40; $shape.Top = 60; $shape.Width = 300; $shape.Height = 200
$keys = Get-PartKeys $shape
Write-IskInfo ("break chart parts: " + ($keys -join ', '))
Assert-Isk $S (@($keys | Where-Object { $_ -like 'axisbreak*' }).Count -ge 1) 'the axis break marks are parts of their own'
Assert-Isk $S ((Get-IskModel $shape) -match 'ybreaks|xbreaks') 'the model stores the break'

# 2. Coordinate system.
$S.Window.Selection.Unselect()
$name2 = $addin.Insert('chart.axes.math'); $axes = $S.Slide.Shapes.Item($name2)
$axes.Left = 380; $axes.Top = 60
Assert-Isk $S ((Get-IskModel $axes) -match 'chart\.math\.axes') 'the coordinate system is a chart type'
Assert-Isk $S ($axes.Type -eq 6 -and $axes.GroupItems.Count -ge 3) 'the coordinate system is drawn natively'

# 3. Function graph, then another function from the Function dropdown (a choice, never a cycle).
$S.Window.Selection.Unselect()
$name3 = $addin.Insert('chart.function.sine'); $fn = $S.Slide.Shapes.Item($name3)
$fn.Left = 380; $fn.Top = 300
$m0 = Get-IskModel $fn
Assert-Isk $S ($m0 -match 'chart\.math\.function') 'the function graph is a chart type'
$fn.Select(); Start-Sleep 2
$addin.Command('pick:function:cubic') | Out-Null; Start-Sleep 3
$fn = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'chart\.math\.function' } | Select-Object -First 1
$m1 = Get-IskModel $fn
Assert-Isk $S ($m1 -ne $m0 -and $m1 -match 'cubic') 'picking another function changes the graph'
Save-IskShot $S '1-breaks-and-functions'
Send-IskEscape; Start-Sleep 1
