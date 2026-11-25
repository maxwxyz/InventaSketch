# Scenario (maintainer question 2026-11-25): BPMN pools with interactive lanes - "Add lane below" grows the pool by one
# lane, "Remove bottom lane" takes it away again (one undo step each).
param($S)
$addin = $S.AddIn

function LaneCount($shape) {
    $m = Get-IskModel $shape
    $match = [regex]::Match($m, '"lanes"\s*:\s*\{[^}]*"text"\s*:\s*"([^"]*)"')
    if (-not $match.Success) { $match = [regex]::Match($m, '"lanes"\s*:\s*"([^"]*)"') }
    if (-not $match.Success) { return -1 }
    @($match.Groups[1].Value -split '\\n').Count
}

$S.Window.Selection.Unselect()
$name = $addin.Insert('diag.bpmn.pool'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 100; $shape.Top = 80
$shape.Select(); Start-Sleep 2
$h0 = $shape.Height
$n0 = LaneCount $shape
if ($n0 -lt 0) { $n0 = 2 }   # a new pool has the default two lanes (no stored parameter yet)
Write-IskInfo ("lanes at start: $n0, height $h0")
$addin.Command('act:diag.lanes.add') | Out-Null; Start-Sleep 3
$pool = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'diag\.bpmn\.pool' } | Select-Object -First 1
$n1 = LaneCount $pool
Write-IskInfo ("lanes after add: $n1, height $($pool.Height)")
Assert-Isk $S ($n1 -eq $n0 + 1) 'Add lane adds one lane'
Assert-Isk $S ($pool.Height -gt $h0 + 1) 'the pool grows by the lane'
Save-IskShot $S '1-lane-added'
$pool.Select(); Start-Sleep 2
$addin.Command('act:diag.lanes.remove') | Out-Null; Start-Sleep 3
$pool = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'diag\.bpmn\.pool' } | Select-Object -First 1
Assert-Isk $S ((LaneCount $pool) -eq $n0) 'Remove lane takes it away again'
Send-IskEscape; Start-Sleep 1
