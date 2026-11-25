# UI/UX review harness (maintainer 2026-11-25: "do the UI/UX review for all elements, combinations and workflows").
# Not a pass/fail test: for one representative item per category (two different types where a category has them), for
# typical combinations and for the main workflows it records what the user sees - a screenshot of the selected element
# with its bar and handles, a screenshot of the pie menu - plus the bar ids, the pie entries and the handle list in
# audit.jsonl. The review reads the pictures and the records.
param($S)
$addin = $S.AddIn
$records = Join-Path $S.OutDir 'audit.jsonl'

function Get-Pie {
    try { [string]$addin.PieItems() } catch { '' }
}
function Get-Hand {
    try { [string]$addin.Handles() } catch { '' }
}
function New-AuditSlide {
    $n = $S.Window.Presentation.Slides.Count + 1
    $sl = $S.Window.Presentation.Slides.Add($n, 12)
    $S.Window.View.GotoSlide($n); Start-Sleep -Milliseconds 600
    $sl
}
function Shot-Around($name, $shapeName, [int]$margin = 260) {
    $r = Get-IskRect $S $shapeName
    $bar = $null; try { $bar = ([string]$addin.BarRect()) -split ',' } catch { }
    $x0 = $r.X; $y0 = $r.Y; $x1 = $r.X + $r.W; $y1 = $r.Y + $r.H
    if ($bar -and $bar.Count -eq 4) {
        $x0 = [Math]::Min($x0, [int]$bar[0]); $y0 = [Math]::Min($y0, [int]$bar[1])
        $x1 = [Math]::Max($x1, [int]$bar[0] + [int]$bar[2]); $y1 = [Math]::Max($y1, [int]$bar[1] + [int]$bar[3])
    }
    Save-IskShot $S $name -Around ($x0 - $margin / 2), ($y0 - $margin / 2), ($x1 - $x0 + $margin), ($y1 - $y0 + $margin)
}
function Write-Record($kind, $id, $extra) {
    $rec = [ordered]@{ kind = $kind; id = $id; bar = [string]$addin.BarIds(); handles = (Get-Hand); pie = (Get-Pie) }
    if ($extra) { foreach ($k in $extra.Keys) { $rec[$k] = $extra[$k] } }
    ($rec | ConvertTo-Json -Compress -Depth 4) | Add-Content -Path $records -Encoding UTF8
}
function Open-Pie($shapeName) {
    $r = Get-IskRect $S $shapeName
    Move-IskMouse $r.CX $r.CY; Start-Sleep -Milliseconds 400
    [IskTest.Native]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100
    [IskTest.Native]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 1300
}
function Audit-Selected($tag, $shapeName, [switch]$Pie) {
    Start-Sleep -Milliseconds 1800
    Shot-Around ("sel-" + $tag) $shapeName
    if ($Pie) {
        Open-Pie $shapeName
        $r = Get-IskRect $S $shapeName
        Save-IskShot $S ("pie-" + $tag) -Around ($r.CX - 330), ($r.CY - 330), 660, 700
    }
    Write-Record 'element' $tag $null
    if ($Pie) { Send-IskEscape; Start-Sleep -Milliseconds 500 }
}

# ---------------------------------------------------------------- 1. one or two items per category
$catalog = @(([string]$addin.LibraryCatalog() -split "`n") | Where-Object { $_ } | ForEach-Object {
    $p = $_ -split "`t"; [pscustomobject]@{ Id = $p[0]; Cat = $p[1]; Type = $p[2]; Module = $p[3]; Name = $p[4] }
})
Write-IskInfo ("library: {0} items in {1} categories" -f $catalog.Count, @($catalog | Select-Object -ExpandProperty Cat -Unique).Count)
$picked = foreach ($g in ($catalog | Group-Object Cat)) {
    $types = @($g.Group | Group-Object Type | ForEach-Object { $_.Group[0] })
    $types | Select-Object -First 2
}
$i = 0
foreach ($it in $picked) {
    $i++
    try {
        New-AuditSlide | Out-Null
        $S.Window.Selection.Unselect()
        $name = $addin.Insert($it.Id)
        if (-not $name) { Write-IskInfo ("  {0}: not inserted" -f $it.Id); continue }
        $sh = $S.Window.View.Slide.Shapes.Item($name)
        $sh.Left = 300; $sh.Top = 180
        $sh.Select()
        $tag = ('{0:000}-{1}' -f $i, ($it.Id -replace '[^A-Za-z0-9.]', '_'))
        # The pie for the first item of each category only (its structure is fixed; the content differs per type).
        $first = ($i -eq 1) -or ($picked[$i - 2].Cat -ne $it.Cat)
        Audit-Selected $tag $name -Pie:$first
    }
    catch { Write-IskInfo ("  {0}: {1}" -f $it.Id, $_.Exception.Message) }
}

# ---------------------------------------------------------------- 2. combinations
function Combo($tag, [scriptblock]$build) {
    try {
        New-AuditSlide | Out-Null
        $S.Window.Selection.Unselect()
        $focus = & $build
        if ($focus) { $focus.Select(); Audit-Selected ("combo-" + $tag) $focus.Name -Pie }
    }
    catch { Write-IskInfo ("  combo {0}: {1}" -f $tag, $_.Exception.Message) }
}
function Put($id, $x, $y) {
    $n = $addin.Insert($id); $sh = $S.Window.View.Slide.Shapes.Item($n); $sh.Left = $x; $sh.Top = $y; $sh
}
function Fit($target, $id) {
    $target.Select(); Start-Sleep 1
    $addin.Command('ins:' + $id) | Out-Null; Start-Sleep 2
    Get-IskSelected $S
}
Combo 'shaft-gear-bearing' { $sh = Put 'mech.shaft' 250 230; $g = Fit $sh 'mech.gear.spur'; $b = Fit $sh 'mech.bearing.ball'; $g }
Combo 'gear-mesh' { $g = Put 'mech.gear.spur' 250 200; Fit $g 'mech.gear.spur' | Out-Null; $g }
Combo 'pv-cable' {
    $pv = Put 'energy.pv.string' 200 200; $pv.Select(); Start-Sleep 1
    $ports = @(([string]$addin.BuildPorts($pv.Name) -split "`n") | Where-Object { $_ })
    $c = $ports | Where-Object { ($_ -split '\|')[1] -eq 'Connect' } | Select-Object -First 1
    if ($c) { $addin.Command(('port:{0}:{1}' -f ($c -split '\|')[0], ((($c -split '\|')[2]) -split ',')[0])) | Out-Null; Start-Sleep 2 }
    Get-IskSelected $S
}
Combo 'pump-pipe' { $p = Put 'fluid.pump' 250 220; $p }
Combo 'flow-chain' { $b = Put 'ann.flow.process' 250 160; $b }
Combo 'chart-part' { $c = Put 'chart.line' 200 150; $c.Select(); Start-Sleep 1; $addin.Command('partsel:series.0') | Out-Null; $c }
Combo 'chart-limits' { $c = Put 'chart.limits' 200 150; $c }
Combo 'table' { $t = Put 'ann.table' 250 180; $t }
Combo 'wall-door' { $w = Put 'arch.wall' 200 220; Fit $w 'arch.door.single' }
Combo 'resistor' { $r = Put 'elec.resistor' 250 220; $r }

# ---------------------------------------------------------------- 3. multi-selection
try {
    New-AuditSlide | Out-Null
    $a = Put 'mech.gear.spur' 200 200; $b = Put 'ann.flow.process' 480 220
    $a.Select(); $b.Select(0); Start-Sleep 2
    Save-IskShot $S 'multi-selection'
    Write-Record 'multi' 'gear+box' $null
}
catch { Write-IskInfo ("  multi: " + $_.Exception.Message) }

# ---------------------------------------------------------------- 4. workflows: insert box, placement ghost
try {
    New-AuditSlide | Out-Null
    $sh = Put 'mech.shaft' 250 230; $sh.Select(); Start-Sleep 1
    [IskTest.Native]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x20, 0, 0, [UIntPtr]::Zero)
    [IskTest.Native]::keybd_event(0x20, 0, 2, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
    Start-Sleep 2
    Save-IskShot $S 'workflow-insert-box'
    Send-IskEscape; Start-Sleep 1
    $addin.Place('mech.bearing.ball') | Out-Null; Start-Sleep 1
    $r = Get-IskRect $S $sh.Name
    Move-IskMouse ($r.CX + 30) ($r.CY + 40); Start-Sleep 1
    Save-IskShot $S 'workflow-place-ghost-near-shaft' -Around ($r.CX - 300), ($r.CY - 250), 600, 500
    Send-IskEscape; Start-Sleep 1
}
catch { Write-IskInfo ("  workflow: " + $_.Exception.Message) }
Write-IskInfo ("records: " + $records)
