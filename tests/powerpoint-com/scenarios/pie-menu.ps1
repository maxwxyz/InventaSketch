# Scenario (maintainer verdict 2026-11-25: "the pie menu is a catastrophe, the UI/UX explains nothing"): the pie menu of
# a gear, an arrow and a table has the same eight labelled places (text, edit, insert, duplicate, style, delete, view,
# arrange); a submenu opens with "Back"; Esc and a click outside close it; the bar's "..." button opens it too. Pie
# entries are read back through the automation object (PieItems()) or the add-in log ("Pie: ...").
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
$k = Get-IskScale

function Get-PieItems {
    $raw = $null
    try { $raw = [string]$addin.PieItems() } catch { $raw = $null }
    if ($raw) {
        return , @(($raw -split "`n") | Where-Object { $_ } | ForEach-Object { $p = $_ -split "`t"; [pscustomobject]@{ Id = $p[0]; Label = $p[1]; Off = ($p.Count -gt 2) } })
    }
    $log = Get-IskLog $S
    $line = Get-Content $log | Where-Object { $_ -match 'Pie: ' } | Select-Object -Last 1
    if (-not $line) { return , @() }
    , @((($line -replace '.*Pie: ', '') -split '; ') | Where-Object { $_ } | ForEach-Object {
        $eq = $_.IndexOf('=')
        [pscustomobject]@{ Id = $_.Substring(0, $eq); Label = ($_.Substring($eq + 1) -replace ' \(off\)$', ''); Off = ($_ -match ' \(off\)$') }
    })
}

function Invoke-RightClick([double]$x, [double]$y) {
    Move-IskMouse $x $y; Start-Sleep -Milliseconds 400
    [IskTest.Native]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100
    [IskTest.Native]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
}

function Get-LogTail([string]$pattern) {
    $log = Get-IskLog $S
    Get-Content $log | Where-Object { $_ -match $pattern } | Select-Object -Last 1
}

$expected = @('text', 'edit', 'insert', 'duplicate', 'style', 'delete', 'view', 'arrange')

function Test-Pie([string]$what, [string]$name) {
    $r = Get-IskRect $S $name
    Invoke-RightClick $r.CX $r.CY
    $items = Get-PieItems
    Write-IskInfo ("$what pie: " + (($items | ForEach-Object { $_.Id + '=' + $_.Label + $(if ($_.Off) { ' (off)' } else { '' }) }) -join ' | '))
    Assert-Isk $S ($items.Count -eq 8) "$what`: the pie has its eight places ($($items.Count))"
    Assert-Isk $S (@($items | Where-Object { -not $_.Label -or $_.Label.Trim().Length -eq 0 }).Count -eq 0) "$what`: every place has a label"
    if ($items.Count -eq 8) {
        Assert-Isk $S ($items[2].Id -eq 'insert' -and $items[3].Id -eq 'dup' -and $items[5].Id -eq 'delete') "$what`: insert right, duplicate bottom right, delete bottom left"
        Assert-Isk $S ($items[1].Id -eq 'menu:g:edit' -and $items[4].Id -eq 'menu:g:style' -and $items[7].Id -eq 'menu:g:arrange') "$what`: edit, style and arrange keep their places"
    }
    # Hover over the edit place: the one-line hint below the ring (screenshot).
    Move-IskMouse ($r.CX + 60 * $k) ($r.CY - 60 * $k); Start-Sleep -Milliseconds 800
    Save-IskShot $S ("pie-" + $what) -Around ($r.CX - 260), ($r.CY - 260), 520, 560
    $items
}

# 1) A gear: open, a submenu with Back, Esc closes.
$g = $addin.Insert('mech.spur.axial'); $gs = $S.Slide.Shapes.Item($g); $gs.Left = 300; $gs.Top = 180
$gs.Select(); Start-Sleep 3
$ids = Get-IskBarIds $S
Write-IskInfo ('gear bar: ' + ($ids -join ' '))
Assert-Isk $S ($ids.Count -ge 2 -and $ids.Count -le 5 -and $ids[$ids.Count - 1] -eq 'more') 'the bar has at most four buttons and ends with "..." (more)'
Assert-Isk $S (($ids -notcontains 'delete') -and ($ids -notcontains 'dup')) 'delete and duplicate live in the pie (Del, Ctrl+D)'
$items = Test-Pie 'gear' $g
$titleLine = Get-LogTail 'Pie \[' 
Assert-Isk $S ($titleLine -match 'Pie \[[^\]]+\]') "the pie names the element in its centre ($([regex]::Match([string]$titleLine, 'Pie \[[^\]]+\]').Value))"
$r = Get-IskRect $S $g
# Click the "edit" place (upper right): the submenu opens with "Back" first.
Invoke-IskClick ($r.CX + 75 * $k) ($r.CY - 75 * $k) -WaitMs 1200
$sub = Get-PieItems
Write-IskInfo ('edit submenu: ' + (($sub | ForEach-Object { $_.Id }) -join ' '))
Assert-Isk $S ($sub.Count -ge 2 -and $sub[0].Id -eq 'menu:back') 'the edit submenu opens with Back first'
Save-IskShot $S 'pie-gear-edit' -Around ($r.CX - 260), ($r.CY - 260), 520, 560
Send-IskEscape; Start-Sleep 1
Assert-Isk $S ((Get-LogTail 'Cancel Pie') -match 'escape') 'Esc closes the pie'

# 2) Click outside closes it.
$gs = Get-IskShapes $S 'ISK:mech.gear*' | Select-Object -First 1
$gs.Select(); Start-Sleep 2
$r = Get-IskRect $S $gs.Name
Invoke-RightClick $r.CX $r.CY
Invoke-IskClick ($r.CX + 400 * $k) ($r.CY + 200 * $k) -WaitMs 1200
Assert-Isk $S ((Get-LogTail 'Cancel Pie') -match 'click outside') 'a click outside closes the pie'

# 3) The bar's "..." opens the same pie.
$gs = Get-IskShapes $S 'ISK:mech.gear*' | Select-Object -First 1
$gs.Select(); Start-Sleep 2
$addin.Command('more') | Out-Null; Start-Sleep 2
Assert-Isk $S ((Get-PieItems).Count -eq 8) 'the bar button "..." opens the pie'
Send-IskEscape; Start-Sleep 1

# 4) An arrow and a table: the same places.
$S.Window.Selection.Unselect()
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
$a = $addin.Insert('ann.arrow'); $as = $S.Slide.Shapes.Item($a); $as.Left = 300; $as.Top = 200
$as.Select(); Start-Sleep 3
$items = Test-Pie 'arrow' $a
Send-IskEscape; Start-Sleep 1
$S.Window.Selection.Unselect()
$t = $addin.Insert('ann.table'); $ts = $S.Slide.Shapes.Item($t); $ts.Left = 300; $ts.Top = 320
$ts.Select(); Start-Sleep 3
$items = Test-Pie 'table' $t
Send-IskEscape; Start-Sleep 1
