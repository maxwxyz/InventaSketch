# Scenario (charts, maintainer 2026-11-25: "click+drag on a curve moves it in the chart, values adjusted in the
# background"): an XY chart, its curve selected; pressing on the curve between two points and dragging up moves the whole
# curve: every y value changes by the same snapped offset, x stays (a mostly vertical drag); one Ctrl+Z restores it. A drag
# on the chart while no curve is selected still moves the chart (PowerPoint's move).
param($S)
$addin = $S.AddIn
$k = Get-IskScale

function Get-Points($shape) {
    $m = [regex]::Match((Get-IskModel $shape), '"data"\s*:\s*"([^"]*)"')
    $rows = if ($m.Success) { @(($m.Groups[1].Value -replace '\\n', "`n") -split "`n" | Where-Object { $_ -match '^\s*-?[\d.]+\s*;' }) } else { @() }
    , @($rows | ForEach-Object { $f = $_ -split ';'; [pscustomobject]@{ X = [double]$f[0]; Y = [double]$f[1] } })
}
function Get-Chart { Get-IskShapes $S 'ISK:chart*' | Select-Object -First 1 }
function Get-Grip([string]$name, [string]$key) {
    foreach ($part in (([string]$addin.DragHandlesOf($name)) -split ';' | Where-Object { $_ })) {
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
$p0 = Get-Points $chart
$r = Get-IskRect $S $chart.Name
$around = @(($r.X - 40), ($r.Y - 60), ($r.W + 80), ($r.H + 120))
$addin.Command('partsel:series.0') | Out-Null; Start-Sleep 2
Assert-Isk $S ([string]$addin.SelectedPart() -eq 'series.0') 'the curve is selected'

# A place on the curve halfway between points 3 and 4 (no point grip there).
$a = Get-Grip $chart.Name 'point.0.2'; $b = Get-Grip $chart.Name 'point.0.3'
Assert-Isk $S ($null -ne $a -and $null -ne $b) 'the curve shows its point grips'
if ($null -eq $a -or $null -eq $b) { return }
$mx = ($a.X + $b.X) / 2; $my = ($a.Y + $b.Y) / 2
Write-IskInfo ("press on the curve at $([int]$mx),$([int]$my)")
Invoke-IskDrag -From @($mx, $my) -To @(($mx + 3 * $k), ($my - 45 * $k)) -BeforeRelease { Save-IskShot $S '1-dragging-curve' -Around $around }
Start-Sleep 2
$chart = Get-Chart
$p1 = Get-Points $chart
Write-IskInfo ("before: " + (($p0 | ForEach-Object { "$($_.X);$($_.Y)" }) -join ' ') + "  after: " + (($p1 | ForEach-Object { "$($_.X);$($_.Y)" }) -join ' '))
Assert-Isk $S ($p1.Count -eq $p0.Count) 'the curve keeps its points'
$dy = @(for ($i = 0; $i -lt $p1.Count; $i++) { [math]::Round($p1[$i].Y - $p0[$i].Y, 6) }) | Select-Object -Unique
$dx = @(for ($i = 0; $i -lt $p1.Count; $i++) { [math]::Round($p1[$i].X - $p0[$i].X, 6) }) | Select-Object -Unique
Assert-Isk $S (@($dy).Count -eq 1 -and $dy[0] -gt 0) "every y value moved up by the same offset ($($dy -join ','))"
Assert-Isk $S (@($dx).Count -eq 1 -and $dx[0] -eq 0) 'x stays for a mostly vertical drag'
Assert-Isk $S (([string]$dy[0]) -match '^\d+(\.\d{1,2})?$') "the offset is snapped ($($dy[0]))"
Assert-Isk $S ([math]::Abs($chart.Left - 160) -lt 0.5 -and [math]::Abs($chart.Top - 100) -lt 0.5) 'the chart itself did not move'
Save-IskShot $S '2-curve-moved' -Around $around

Send-IskCtrlKey 0x5A; Start-Sleep 2
$p2 = Get-Points (Get-Chart)
Assert-Isk $S ((($p2 | ForEach-Object { $_.Y }) -join ',') -eq (($p0 | ForEach-Object { $_.Y }) -join ',')) 'one Ctrl+Z restores the curve'
Send-IskEscape; Start-Sleep 1
