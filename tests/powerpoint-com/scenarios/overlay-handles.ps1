# Scenario (maintainer report 2026-11-25: "on a pipe/cable line all overlay handles sit on top of each other"): a short
# cable docked on a PV string (lock badge at the docked end, "+" at the free end), a short free pipe and a gear with a
# meshing partner. The handles of each are read back (automation Handles(), else the add-in log line "Handles: ...")
# and must not overlap; a free line end shows exactly one "+".
param($S)
$addin = $S.AddIn
$k = Get-IskScale

function Get-Handles {
    $raw = $null
    try { $raw = [string]$addin.Handles() } catch { $raw = $null }
    if (-not $raw) {
        $log = Get-IskLog $S
        $line = Get-Content $log | Where-Object { $_ -match 'Handles: ' } | Select-Object -Last 1
        if ($line) {
            Assert-Isk $S ($line -notmatch 'overlap!') 'the add-in logs no overlapping handles'
            $raw = ($line -replace '.*Handles: ', '') -replace ' \(overlap!\)\s*$', ''
        }
    }
    , @(($raw -split ';') | Where-Object { $_ } | ForEach-Object {
        $m = [regex]::Match($_, '^(.+)@(-?\d+),(-?\d+),([\d.]+)$')
        if ($m.Success) { [pscustomobject]@{ Id = $m.Groups[1].Value; X = [double]$m.Groups[2].Value; Y = [double]$m.Groups[3].Value; R = [double]$m.Groups[4].Value } }
    })
}

function Test-NoOverlap([string]$what) {
    $h = Get-Handles
    Write-IskInfo ("$what handles: " + (($h | ForEach-Object { '{0}@{1},{2}' -f $_.Id, $_.X, $_.Y }) -join ' '))
    Assert-Isk $S ($h.Count -gt 0) "$what shows handles"
    $bad = @()
    for ($i = 0; $i -lt $h.Count; $i++) {
        for ($j = $i + 1; $j -lt $h.Count; $j++) {
            $a = $h[$i]; $b = $h[$j]
            # Point diamonds sit on the geometry (fixed); everything else keeps clear of them and of each other.
            if ($a.Id -like 'pt:*' -and $b.Id -like 'pt:*') { continue }
            $d = Get-IskDistance @($a.X, $a.Y) @($b.X, $b.Y)
            if ($d -lt ($a.R + $b.R) * $k - 1) { $bad += ('{0}/{1} {2:0}px' -f $a.Id, $b.Id, $d) }
        }
    }
    Assert-Isk $S ($bad.Count -eq 0) ("$what`: no two handles overlap " + ($bad -join ', '))
    $plus = @($h | Where-Object { $_.Id -like 'port:*' -or $_.Id -like 'end:*' -or $_.Id -like 'side:*' })
    $twin = @()
    for ($i = 0; $i -lt $plus.Count; $i++) {
        for ($j = $i + 1; $j -lt $plus.Count; $j++) {
            if ((Get-IskDistance @($plus[$i].X, $plus[$i].Y) @($plus[$j].X, $plus[$j].Y)) -lt 30 * $k) { $twin += ($plus[$i].Id + '/' + $plus[$j].Id) }
        }
    }
    Assert-Isk $S ($twin.Count -eq 0) ("$what`: one + per place " + ($twin -join ', '))
    $h
}

# 1) A short cable docked on a PV string: lock badge at the docked end, one "+" at the free end.
$S.Window.Selection.Unselect()
$pv = $addin.Insert('energy.pv.string.10'); $shape = $S.Slide.Shapes.Item($pv)
$shape.Left = 120; $shape.Top = 160
$shape.Select(); Start-Sleep 2
$ports = @(($addin.BuildPorts($pv) -split "`n") | Where-Object { $_ })
$connect = $ports | Where-Object { ($_ -split '\|')[1] -eq 'Connect' } | Select-Object -First 1
Assert-Isk $S ($null -ne $connect) 'the PV string offers a connection "+"'
if ($connect) {
    $cid = ($connect -split '\|')[0]; $item = (($connect -split '\|')[2] -split ',')[0]
    $addin.Command("port:${cid}:$item") | Out-Null; Start-Sleep 3
    $cable = Get-IskSelected $S
    # Short: about 15 mm on the slide.
    if ($cable.Width -gt $cable.Height) { $cable.Width = 42 } else { $cable.Height = 42 }
    Start-Sleep 2
    $S.Window.Selection.Unselect(); Start-Sleep 1
    $cable.Select(); Start-Sleep 3
    $r = Get-IskRect $S $cable.Name
    Save-IskShot $S '1-short-cable' -Around ($r.CX - 200), ($r.CY - 200), 400, 400
    $h = Test-NoOverlap 'short cable'
    Assert-Isk $S (@($h | Where-Object { $_.Id -like 'lock:*' }).Count -ge 1) 'the docked end shows its lock badge'
    Assert-Isk $S (@($h | Where-Object { $_.Id -like 'port:*' -or $_.Id -like 'end:*' }).Count -eq 1) 'the free end shows exactly one "+"'
    # Hover over the "+": its tooltip (screenshot).
    $plus = $h | Where-Object { $_.Id -like 'port:*' -or $_.Id -like 'end:*' } | Select-Object -First 1
    if ($plus) { Move-IskMouse $plus.X $plus.Y; Start-Sleep -Milliseconds 800; Save-IskShot $S '2-plus-tip' -Around ($r.CX - 200), ($r.CY - 200), 500, 400 }
}

# 2) A short free pipe (both ends free): diamonds, one "+" per end, nothing on top of PowerPoint's handles.
$S.Window.Selection.Unselect()
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
$pipe = $addin.Insert('fluid.line'); $ps = $S.Slide.Shapes.Item($pipe)
$ps.Left = 300; $ps.Top = 250
if ($ps.Width -gt $ps.Height) { $ps.Width = 36 } else { $ps.Height = 36 }
Start-Sleep 2
$S.Window.Selection.Unselect(); Start-Sleep 1
$ps = Get-IskShapes $S 'ISK:fluid*' | Select-Object -First 1
$ps.Select(); Start-Sleep 3
$r = Get-IskRect $S $ps.Name
Save-IskShot $S '3-short-pipe' -Around ($r.CX - 200), ($r.CY - 200), 400, 400
$h = Test-NoOverlap 'short pipe'
Assert-Isk $S (@($h | Where-Object { $_.Id -like 'port:*' -or $_.Id -like 'end:*' }).Count -eq 2) 'two free ends, two "+"'

# 3) A gear with a meshing partner: build "+" on the free sides and the lock badge of the mesh do not collide.
$S.Window.Selection.Unselect()
foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
$g = $addin.Insert('mech.spur.axial'); $gs = $S.Slide.Shapes.Item($g); $gs.Left = 200; $gs.Top = 150
$gs.Select(); Start-Sleep 2
$addin.Command('port:mesh.right:mech.spur.axial') | Out-Null; Start-Sleep 5
$S.Window.Selection.Unselect(); Start-Sleep 1
(Get-IskShapes $S 'ISK:mech.gear.spur*' | Select-Object -First 1).Select(); Start-Sleep 3
$r = Get-IskRect $S (Get-IskSelected $S).Name
Save-IskShot $S '4-gear' -Around ($r.X - 120), ($r.Y - 120), ($r.W + 240), ($r.H + 240)
$h = Test-NoOverlap 'gear'
Assert-Isk $S (@($h | Where-Object { $_.Id -like 'lock:*' }).Count -ge 1) 'the mesh shows a lock badge'
Send-IskEscape; Start-Sleep 1
