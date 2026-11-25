# Scenario (charts, maintainer 2026-11-25: "select the values/points of every curve, highlighted in blue; single points
# can be moved by click and drag or deleted"): an XY chart; selecting the curve shows its point grips; a click on a point
# grip selects that point (part "point.0.3", it glows); dragging it changes the value (snapped) in one undo step; Del
# deletes the selected point (not the chart) and leaves the curve selected.
param($S)
$addin = $S.AddIn
$k = Get-IskScale

function Get-Data($shape) {
    $m = [regex]::Match((Get-IskModel $shape), '"data"\s*:\s*"([^"]*)"')
    if ($m.Success) { $m.Groups[1].Value -replace '\\n', "`n" } else { '' }
}
function Get-Rows($shape) { , @((Get-Data $shape) -split "`n" | Where-Object { $_ -match '^\s*-?[\d.]+\s*;' }) }
function Get-Chart { Get-IskShapes $S 'ISK:chart*' | Select-Object -First 1 }
function Get-Grip([string]$name, [string]$key) {
    $raw = [string]$addin.DragHandlesOf($name)
    foreach ($part in ($raw -split ';' | Where-Object { $_ })) {
        $m = [regex]::Match($part, '^(.+):(-?[\d.]+),(-?[\d.]+)$')
        if ($m.Success -and $m.Groups[1].Value -eq $key) {
            $px = ConvertTo-IskPixel $S @(([double]$m.Groups[2].Value * 25.4 / 72), ([double]$m.Groups[3].Value * 25.4 / 72))
            return [pscustomobject]@{ X = $px[0]; Y = $px[1] }
        }
    }
    $null
}

$S.Window.Selection.Unselect()
$name = $addin.Insert('chart.line'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 160; $shape.Top = 100; $shape.Width = 480; $shape.Height = 300
Start-Sleep 2
$chart = Get-Chart
$chart.Select(); Start-Sleep 2
$rows0 = Get-Rows $chart
Write-IskInfo ("rows before: " + ($rows0 -join ' | '))
Assert-Isk $S ($rows0.Count -eq 7) 'the line chart has seven data points'
$r = Get-IskRect $S $chart.Name
$around = @(($r.X - 40), ($r.Y - 60), ($r.W + 80), ($r.H + 120))

# 1) The curve selected: its point grips are shown (handle list), the bar names the curve.
$addin.Command('partsel:series.0') | Out-Null; Start-Sleep 2
$shown = [string]$addin.ShownHandles()
Write-IskInfo ("shown handles: " + $shown)
Save-IskShot $S '1-curve-selected' -Around $around
$g = Get-Grip $chart.Name 'point.0.3'
Assert-Isk $S ($null -ne $g) 'point 4 of the curve has a grip'
if ($null -eq $g) { return }

# 2) A click on the grip selects the point (it glows), the bar shows the point's commands.
Invoke-IskClick $g.X $g.Y; Start-Sleep 2
$part = [string]$addin.SelectedPart()
Write-IskInfo ("selected part: $part; bar: " + $addin.BarIds())
Assert-Isk $S ($part -eq 'point.0.3') "a click on the grip selects the point ($part)"
Assert-Isk $S (($addin.BarIds() -split ',') -contains 'part:part.delete') 'the point bar offers "Delete point"'
Save-IskShot $S '2-point-selected' -Around $around

# 3) Drag the point up by about 40 px: its y value changes (snapped), x stays, the other points stay.
$chart = Get-Chart; $chart.Select(); Start-Sleep 1
$addin.Command('partsel:point.0.3') | Out-Null; Start-Sleep 1
$g = Get-Grip $chart.Name 'point.0.3'
Invoke-IskDrag -From @($g.X, $g.Y) -To @($g.X, ($g.Y - 40 * $k)) -BeforeRelease { Save-IskShot $S '3-dragging' -Around $around }
Start-Sleep 2
$chart = Get-Chart
$rows1 = Get-Rows $chart
Write-IskInfo ("rows after drag: " + ($rows1 -join ' | '))
Assert-Isk $S ($rows1.Count -eq 7 -and $rows1[3] -ne $rows0[3]) 'dragging the point changes its value'
Assert-Isk $S (($rows1[3] -split ';')[0].Trim() -eq '3') 'the point keeps its x (3)'
Assert-Isk $S ((($rows1[3] -split ';')[1].Trim()) -match '^-?\d+(\.\d{1,2})?$') "the new value is snapped ($($rows1[3]))"
Assert-Isk $S ($rows1[2] -eq $rows0[2] -and $rows1[4] -eq $rows0[4]) 'the neighbours are unchanged'
Send-IskCtrlKey 0x5A; Start-Sleep 2
Assert-Isk $S ((Get-Rows (Get-Chart))[3] -eq $rows0[3]) 'one Ctrl+Z restores the point'

# 4) Del deletes the selected point (the chart stays), and the curve stays selected.
$chart = Get-Chart; $chart.Select(); Start-Sleep 2
$addin.Command('partsel:point.0.3') | Out-Null; Start-Sleep 1
[IskTest.Native]::keybd_event(0x2E, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x2E, 0, 2, [UIntPtr]::Zero)
Start-Sleep 3
$chart = Get-Chart
Assert-Isk $S ($null -ne $chart) 'Del did not delete the chart'
$rows2 = Get-Rows $chart
Write-IskInfo ("rows after Del: " + ($rows2 -join ' | ') + "; selected part: " + $addin.SelectedPart())
Assert-Isk $S ($rows2.Count -eq 6 -and -not ($rows2 -contains $rows0[3])) 'Del deleted exactly the selected point'
Assert-Isk $S ([string]$addin.SelectedPart() -eq 'series.0') 'the curve stays selected after the point is deleted'
Save-IskShot $S '4-point-deleted' -Around $around
Send-IskEscape; Start-Sleep 1
