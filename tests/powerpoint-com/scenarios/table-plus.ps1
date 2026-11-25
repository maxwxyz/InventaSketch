# Scenario (maintainer 2026-11-25): a selected table shows one blue "+" on each side; one click adds a row at the top
# (under the header row) or bottom, a column on the left or right. The "+" positions come from the automation object
# (Handles()) or the add-in log ("Handles: ..."); without them the side commands run through Command().
param($S)
$addin = $S.AddIn

function Get-Handles {
    $raw = $null
    try { $raw = [string]$addin.Handles() } catch { $raw = $null }
    if (-not $raw) {
        $log = Get-IskLog $S
        $line = Get-Content $log | Where-Object { $_ -match 'Handles: ' } | Select-Object -Last 1
        if ($line) { $raw = ($line -replace '.*Handles: ', '') -replace ' \(overlap!\)\s*$', '' }
    }
    , @(($raw -split ';') | Where-Object { $_ } | ForEach-Object {
        $m = [regex]::Match($_, '^(.+)@(-?\d+),(-?\d+),([\d.]+)$')
        if ($m.Success) { [pscustomobject]@{ Id = $m.Groups[1].Value; X = [double]$m.Groups[2].Value; Y = [double]$m.Groups[3].Value } }
    })
}

function Get-Cells {
    $t = Get-IskShapes $S 'ISK:ann.table*' | Select-Object -First 1
    $model = Get-IskModel $t
    $m = [regex]::Match($model, '"cells"\s*:\s*"((?:[^"\\]|\\.)*)"')
    if (-not $m.Success) { return @{ Rows = 3; Cols = 2; Raw = '' } }   # default cells: three rows, two columns
    $raw = $m.Groups[1].Value -replace '\\n', "`n"
    $rows = @($raw -split "`n" | Where-Object { $_ -ne '' })
    @{ Rows = $rows.Count; Cols = (($rows[0] -split '\|').Count); Raw = $raw }
}

function Select-Table { (Get-IskShapes $S 'ISK:ann.table*' | Select-Object -First 1).Select(); Start-Sleep 3 }

$t = $addin.Insert('ann.table'); $ts = $S.Slide.Shapes.Item($t); $ts.Left = 300; $ts.Top = 200
Select-Table
$r = Get-IskRect $S (Get-IskSelected $S).Name
Save-IskShot $S '1-table-plus' -Around ($r.X - 120), ($r.Y - 140), ($r.W + 240), ($r.H + 260)
$h = Get-Handles
Write-IskInfo ('table handles: ' + (($h | ForEach-Object { '{0}@{1},{2}' -f $_.Id, $_.X, $_.Y }) -join ' '))
foreach ($side in 'top', 'bottom', 'left', 'right') {
    Assert-Isk $S (@($h | Where-Object { $_.Id -eq "side:$side" }).Count -eq 1) "a + on the $side side"
}
$ids = Get-IskBarIds $S
Assert-Isk $S (($ids -notcontains 'act:ann.table.row.add') -and ($ids -notcontains 'act:ann.table.column.add')) 'adding rows/columns is on the table, not in the bar'

$steps = @(
    @{ Side = 'top'; Command = 'act:ann.table.row.add.top'; Rows = 1; Cols = 0 },
    @{ Side = 'bottom'; Command = 'act:ann.table.row.add'; Rows = 1; Cols = 0 },
    @{ Side = 'left'; Command = 'act:ann.table.column.add.left'; Rows = 0; Cols = 1 },
    @{ Side = 'right'; Command = 'act:ann.table.column.add'; Rows = 0; Cols = 1 }
)
foreach ($st in $steps) {
    Select-Table
    $c0 = Get-Cells
    $plus = (Get-Handles) | Where-Object { $_.Id -eq ('side:' + $st.Side) } | Select-Object -First 1
    if ($plus) { Invoke-IskClick $plus.X $plus.Y -WaitMs 3000 } else { $addin.Command($st.Command) | Out-Null; Start-Sleep 3 }
    $c1 = Get-Cells
    Write-IskInfo ("{0}: {1}x{2} -> {3}x{4}" -f $st.Side, $c0.Rows, $c0.Cols, $c1.Rows, $c1.Cols)
    Assert-Isk $S ($c1.Rows -eq $c0.Rows + $st.Rows -and $c1.Cols -eq $c0.Cols + $st.Cols) ("one click on the {0} + adds a {1}" -f $st.Side, $(if ($st.Rows) { 'row' } else { 'column' }))
}
# The header stays the first row: the top "+" added under it.
$cells = Get-Cells
$headerRow = @($cells.Raw -split "`n")[0]
Assert-Isk $S ($headerRow.Trim('|').Length -gt 0) "the header row stays on top ($headerRow)"
Select-Table
$r = Get-IskRect $S (Get-IskSelected $S).Name
Save-IskShot $S '2-table-grown' -Around ($r.X - 120), ($r.Y - 140), ($r.W + 240), ($r.H + 260)
