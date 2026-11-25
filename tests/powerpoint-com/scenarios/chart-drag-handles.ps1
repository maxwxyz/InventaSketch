# Scenario (direct manipulation, 2026-11-25): an XY chart with limit lines 1.5 and 5.5 shows a grip on each limit line;
# dragging a grip moves the line vertically, the value snaps (a tenth of the axis step) and is stored in the model's
# "limits" parameter in one undo step; Esc during the drag cancels. Grip positions come from the automation object
# (DragHandlesOf, slide points) or the handle list (Handles() / log line "Handles: ... drag:i@x,y").
param($S)
$addin = $S.AddIn
$k = Get-IskScale

function Get-Limits {
    $c = Get-IskShapes $S 'ISK:chart*' | Select-Object -First 1
    $m = [regex]::Match((Get-IskModel $c), '"limits"\s*:\s*"([^"]*)"')
    if ($m.Success) { $m.Groups[1].Value } else { '' }
}

function Get-Grips([string]$name) {
    $list = @()
    $raw = $null
    try { $raw = [string]$addin.DragHandlesOf($name) } catch { $raw = $null }
    if ($raw) {
        foreach ($part in ($raw -split ';' | Where-Object { $_ })) {
            $m = [regex]::Match($part, '^(.+):(-?[\d.]+),(-?[\d.]+)$')
            # Only the limit lines (the chart also has point and curve handles since 2026-11-25).
            if ($m.Success -and $m.Groups[1].Value -like 'limit.*') {
                $px = ConvertTo-IskPixel $S @(([double]$m.Groups[2].Value * 25.4 / 72), ([double]$m.Groups[3].Value * 25.4 / 72))
                $list += [pscustomobject]@{ Id = $m.Groups[1].Value; X = $px[0]; Y = $px[1] }
            }
        }
        return , $list
    }
    $h = $null
    try { $h = [string]$addin.Handles() } catch { $h = $null }
    if (-not $h) {
        $line = Get-Content (Get-IskLog $S) | Where-Object { $_ -match 'Handles: ' } | Select-Object -Last 1
        if ($line) { $h = ($line -replace '.*Handles: ', '') -replace ' \(overlap!\)\s*$', '' }
    }
    foreach ($part in (($h -split ';') | Where-Object { $_ -like 'drag:*' })) {
        $m = [regex]::Match($part, '^(.+)@(-?\d+),(-?\d+),')
        if ($m.Success) { $list += [pscustomobject]@{ Id = $m.Groups[1].Value; X = [double]$m.Groups[2].Value; Y = [double]$m.Groups[3].Value } }
    }
    , $list
}

$name = $addin.Insert('chart.limits'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 200; $shape.Top = 120; $shape.Width = 420; $shape.Height = 280
Start-Sleep 2
$S.Window.Selection.Unselect(); Start-Sleep 1
$chart = Get-IskShapes $S 'ISK:chart*' | Select-Object -First 1
$chart.Select(); Start-Sleep 3
$before = Get-Limits
Write-IskInfo "limits before: $before"
$grips = Get-Grips $chart.Name
Write-IskInfo ('grips: ' + (($grips | ForEach-Object { '{0}@{1:0},{2:0}' -f $_.Id, $_.X, $_.Y }) -join ' '))
Assert-Isk $S ($grips.Count -ge 2) 'each limit line has a grip'
if ($grips.Count -lt 1) { return }
$r = Get-IskRect $S $chart.Name
$g0 = $grips | Sort-Object Y | Select-Object -Last 1   # the lower line (1.5)
Move-IskMouse $g0.X $g0.Y; Start-Sleep -Milliseconds 900
Save-IskShot $S '1-grip-hover' -Around ($r.X - 40), ($r.Y - 60), ($r.W + 80), ($r.H + 120)

# 1) Esc during the drag: nothing changes.
Move-IskMouse $g0.X $g0.Y; Start-Sleep -Milliseconds 400
[IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150
for ($i = 1; $i -le 8; $i++) { Move-IskMouse $g0.X ($g0.Y - 5 * $i * $k); Start-Sleep -Milliseconds 60 }
Send-IskEscape; Start-Sleep -Milliseconds 400
[IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep 2
Assert-Isk $S ((Get-Limits) -eq $before) "Esc cancels the drag ($(Get-Limits))"

# 2) Drag the grip up by about 40 px (horizontal pointer motion is ignored: the line moves vertically only).
$chart = Get-IskShapes $S 'ISK:chart*' | Select-Object -First 1
$chart.Select(); Start-Sleep 2
$grips = Get-Grips $chart.Name
$g0 = $grips | Sort-Object Y | Select-Object -Last 1
Invoke-IskDrag -From @($g0.X, $g0.Y) -To @(($g0.X + 30 * $k), ($g0.Y - 40 * $k)) -BeforeRelease { Save-IskShot $S '2-dragging' -Around ($r.X - 40), ($r.Y - 60), ($r.W + 80), ($r.H + 120) }
$after = Get-Limits
Write-IskInfo "limits after: $after"
Assert-Isk $S ($after -ne $before -and $after.Length -gt 0) 'dragging the grip changes the limit value'
$values = @($after -split '[;\s]+' | Where-Object { $_ })
Assert-Isk $S ($values.Count -eq 2) 'still two limit lines'
Assert-Isk $S (@($values | Where-Object { $_ -notmatch '^-?\d+([.,]\d{1,3})?$' }).Count -eq 0) "the values are snapped ($after)"
Assert-Isk $S ($values -contains '5.5' -or $values -contains '5,5') 'the other limit line is unchanged'
Save-IskShot $S '3-after' -Around ($r.X - 40), ($r.Y - 60), ($r.W + 80), ($r.H + 120)

# 3) One undo step brings the old value back.
Send-IskCtrlKey 0x5A; Start-Sleep 2
Assert-Isk $S ((Get-Limits) -eq $before) "one Ctrl+Z restores the limits ($(Get-Limits))"
