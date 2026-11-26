# Scenario (Calc 5), ASCII only: a centrifugal pump at 70 m3/h gets its characteristic chart from its menu; dragging the
# operating point to a lower flow changes the pump's flow (one document change: pump and chart).
param($S)
$addin = $S.AddIn
$cube = [char]0x00B3
function Find($pattern) { @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match $pattern } | Select-Object -First 1 }
function Flow($shape) {
    $v = [string]$addin.Values($shape.Name)
    $m = [regex]::Match($v, 'Vdot[^;]*?=\s*([\d.,]+)')
    if ($m.Success) { [double]::Parse($m.Groups[1].Value.Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture) } else { -1 }
}

[void]$addin.SetValueLevel('explore')
$S.Window.Selection.Unselect()
$name = $addin.Insert('process.pump.centrifugal'); Start-Sleep 1
$pump = $S.Slide.Shapes.Item($name); $pump.Left = 90; Start-Sleep 1
$pump = Find 'process\.pump\.centrifugal'; $pump.Top = 160; Start-Sleep 1
$pump = Find 'process\.pump\.centrifugal'
$res = [string]$addin.SetValue($pump.Name, 'Vdot', "70 m$cube/h"); Start-Sleep 1
Write-IskInfo ("set flow: " + $res.Substring(0, [Math]::Min(120, $res.Length)))
$pump = Find 'process\.pump\.centrifugal'
$v0 = [string]$addin.Values($pump.Name)
Write-IskInfo ("pump values: " + $v0.Substring(0, [Math]::Min(200, $v0.Length)))
$pump.Select(); Start-Sleep 2
$addin.Command('act:values.chart=pump') | Out-Null; Start-Sleep 4
$chart = Find 'chart\.linked'
Assert-Isk $S ($null -ne $chart) 'the pump inserts its linked characteristic chart'
if (-not $chart) { return }
Save-IskShot $S '1-chart'
$raw = [string]$addin.DragHandlesOf($chart.Name)
Write-IskInfo ("chart grips: $raw")
$m = [regex]::Match($raw, '(op\.op):(-?[\d.]+),(-?[\d.]+)')
Assert-Isk $S $m.Success 'the chart has the operating-point grip'
if (-not $m.Success) { return }
$px = ConvertTo-IskPixel $S @(([double]$m.Groups[2].Value * 25.4 / 72), ([double]$m.Groups[3].Value * 25.4 / 72))
$chart.Select(); Start-Sleep 3
Invoke-IskDrag -From @($px[0], $px[1]) -To @(($px[0] - 90), $px[1]) -BeforeRelease { Save-IskShot $S '2-dragging' }
Start-Sleep 4
$pump = Find 'process\.pump\.centrifugal'
$v1 = [string]$addin.Values($pump.Name)
Write-IskInfo ("pump values after: " + $v1.Substring(0, [Math]::Min(200, $v1.Length)))
Assert-Isk $S ($v1 -ne $v0) 'dragging the operating point changed the pump values'
Save-IskShot $S '3-after'
[void]$addin.SetValueLevel('quick')
