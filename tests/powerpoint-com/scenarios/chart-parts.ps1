# Scenario (charts v2 stage 1): parts of a chart. A curve is recoloured with PowerPoint's own tools; after a redraw
# (an action on the chart) only that curve keeps the new colour. A click without dragging on the curve selects that part:
# the bar shows the part's actions ("part:" ids); "part:part.hide" switches the curve off.
param($S)
$addin = $S.AddIn

# Every command of the selected element (bar and pie): automation CommandIds(), else the add-in log line "Commands: ...".
function Get-CommandIds {
    $raw = $null
    try { $raw = [string]$S.AddIn.CommandIds() } catch { $raw = $null }
    if (-not $raw) {
        $line = Get-Content (Get-IskLog $S) | Where-Object { $_ -match 'Commands: ' } | Select-Object -Last 1
        if ($line) { $raw = $line -replace '.*Commands: ', '' }
    }
    , @(($raw -split ',') | Where-Object { $_ })
}
function Wait-CommandIds([scriptblock]$Until, [int]$Seconds = 10) {
    $ids = @(); $end = (Get-Date).AddSeconds($Seconds)
    do { $ids = Get-CommandIds; if (& $Until $ids) { break }; Start-Sleep -Milliseconds 700 } while ((Get-Date) -lt $end)
    , $ids
}

$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 120; $shape.Top = 90; $shape.Width = 420; $shape.Height = 270
$S.Window.Selection.Unselect(); Start-Sleep 2

function Get-PartLeaves($shape, $key) {
    $list = @()
    if ($shape.Type -eq 6) { foreach ($c in $shape.GroupItems) { if ($c.Tags.Item('ISK_PART') -eq $key) { $list += $c } } }
    $list
}

# 1. Every leaf has its part key.
$shape = $S.Slide.Shapes.Item($name)
$keys = @(); foreach ($c in $shape.GroupItems) { $keys += $c.Tags.Item('ISK_PART') }
Write-IskInfo ("part keys: " + (($keys | Sort-Object -Unique) -join ', '))
Assert-Isk $S (($keys -contains 'series.0') -and ($keys -contains 'axis.x') -and ($keys -contains 'grid.major')) 'leaves carry part keys'

# 2. Recolour the curve natively, redraw through an action: the curve stays red, the axes keep their colour.
$curve = @(Get-PartLeaves $shape 'series.0')
Assert-Isk $S ($curve.Count -ge 1) 'the curve is a leaf of its own'
$curve[0].Line.ForeColor.RGB = 255   # red (BGR)
$curve[0].Line.Weight = 3
$shape.Select(); Start-Sleep 2
$addin.Command('act:chart.grid') | Out-Null; Start-Sleep 2
$shape = $S.Slide.Shapes.Item($name)
$curve = @(Get-PartLeaves $shape 'series.0')
$axis = @(Get-PartLeaves $shape 'axis.x')
Save-IskShot $S '1-recoloured-curve'
Write-IskInfo ("after redraw: curve rgb {0} weight {1}; axis rgb {2}" -f $curve[0].Line.ForeColor.RGB, $curve[0].Line.Weight, $axis[0].Line.ForeColor.RGB)
Assert-Isk $S ($curve[0].Line.ForeColor.RGB -eq 255 -and [math]::Abs($curve[0].Line.Weight - 3) -lt 0.2) 'the recoloured curve stays red after a redraw'
Assert-Isk $S ($axis[0].Line.ForeColor.RGB -ne 255) 'the axis keeps its own colour'
Assert-Isk $S (@(Get-PartLeaves $shape 'grid.major').Count -eq 0) 'the grid toggle ran (redraw happened)'

# 3. A click on the curve selects the part: the bar shows part actions.
$shape.Select(); Start-Sleep 2
$c = $curve[0]
# A node of the curve freeform (slide points), converted to mm for the pixel helper.
$n = $c.Nodes.Count
$node = $c.Nodes.Item([int][math]::Max(1, [math]::Floor($n / 2))).Points
$flat = @($node | ForEach-Object { $_ }); $pt = @([double]$flat[0], [double]$flat[1])
$part = $addin.PartAt($name, $pt[0], $pt[1])
Write-IskInfo ("node {0} of {1} at {2:N1} / {3:N1} pt: part '{4}'" -f [math]::Floor($n / 2), $n, $pt[0], $pt[1], $part)
Assert-Isk $S ($part -eq 'series.0') 'the engine finds the curve at one of its nodes'
$scr = ConvertTo-IskPixel $S @(($pt[0] * 25.4 / 72), ($pt[1] * 25.4 / 72))
Invoke-IskClick $scr[0] $scr[1] 2500
Save-IskShot $S '2-part-selected'
$bar = $addin.BarIds()
Write-IskInfo ("bar: " + $bar)
Assert-Isk $S (((Get-CommandIds) -contains 'part:part.hide') -and $bar -match 'info:part') 'the bar shows the actions of the selected curve'
# Tab steps to the next part, Shift+Tab back to the curve.
[IskTest.Native]::keybd_event(0x09, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x09, 0, 2, [UIntPtr]::Zero); Start-Sleep 2
$tabbed = $addin.SelectedPart()
Write-IskInfo ("after Tab: '$tabbed'")
Assert-Isk $S ($tabbed -ne '' -and $tabbed -ne 'series.0') 'Tab selects the next part'
[IskTest.Native]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x09, 0, 0, [UIntPtr]::Zero)
[IskTest.Native]::keybd_event(0x09, 0, 2, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x10, 0, 2, [UIntPtr]::Zero); Start-Sleep 2
Assert-Isk $S ($addin.SelectedPart() -eq 'series.0') 'Shift+Tab goes back to the curve'
$addin.Command('part:part.hide') | Out-Null; Start-Sleep 2
$shape = $S.Slide.Shapes.Item($name)
Save-IskShot $S '3-curve-hidden'
Assert-Isk $S (@(Get-PartLeaves $shape 'series.0').Count -eq 0) 'hide switches the curve off'
Send-IskEscape; Start-Sleep 1
