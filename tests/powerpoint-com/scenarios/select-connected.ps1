# Scenario (maintainer 2026-11-25: "select / show connected elements" and "make clear what is selected"): select connected
# by kind - a PV string with its cables and inverter is one net; a shaft with two gears and a meshing partner moves along;
# three flowchart steps are the following elements (also by Ctrl+double-click); two overlapping boxes: the halo log line
# names only the selected one. Selection counts are read from PowerPoint, halo lines from the add-in log ("Halo: ...").
param($S)
$addin = $S.AddIn
$k = Get-IskScale

function Get-Ports($name) { @(($addin.BuildPorts($name) -split "`n") | Where-Object { $_ }) }
function Units { @(Get-IskShapes $S) }
function Selected { try { $S.Window.Selection.ShapeRange.Count } catch { 0 } }
function SelectedName { try { $S.Window.Selection.ShapeRange.Item(1).Name } catch { '' } }
function Clear-Slide {
    $S.Window.Selection.Unselect()
    foreach ($x in @($S.Slide.Shapes)) { $x.Delete() }
    Start-Sleep 1
}
# Every command of the selected element (bar and pie): automation CommandIds(), else the log line "Commands: ...".
function Get-CommandIds {
    $raw = $null
    try { $raw = [string]$addin.CommandIds() } catch { $raw = $null }
    if (-not $raw) {
        $line = Get-Content (Get-IskLog $S) | Where-Object { $_ -match 'Commands: ' } | Select-Object -Last 1
        if ($line) { $raw = $line -replace '.*Commands: ', '' }
    }
    , @(($raw -split ',') | Where-Object { $_ })
}
function Get-LogTail([string]$pattern) {
    Get-Content (Get-IskLog $S) | Where-Object { $_ -match $pattern } | Select-Object -Last 1
}
# "port:<id>:<item>" for the first port of a kind whose offers match a pattern; $null if there is none.
function Get-PortCommand([string]$name, [string]$kind, [string]$itemPattern) {
    foreach ($p in (Get-Ports $name)) {
        $parts = $p -split '\|'
        if ($parts[1] -ne $kind) { continue }
        $item = (($parts[2] -split ',') | Where-Object { $_ -match $itemPattern } | Select-Object -First 1)
        if ($item) { return "port:$($parts[0]):$item" }
    }
    $null
}

# 1) A PV string, a DC cable, an inverter at its end and an AC cable: selecting the first cable, "Net" selects all.
Clear-Slide
$pv = $addin.Insert('energy.pv.string.10'); $shape = $S.Slide.Shapes.Item($pv); $shape.Left = 60; $shape.Top = 120
$shape.Select(); Start-Sleep 2
$cmd = Get-PortCommand $pv 'Connect' 'cable|line|wire'
Assert-Isk $S ($null -ne $cmd) 'the PV string offers a cable at its connection port'
if ($cmd) {
    $addin.Command($cmd) | Out-Null; Start-Sleep 3
    $cable1 = SelectedName
    $cmd = Get-PortCommand $cable1 'LineEnd' 'inverter'
    if ($cmd) { $addin.Command($cmd) | Out-Null; Start-Sleep 3 }
    $inverter = SelectedName
    $cmd = Get-PortCommand $inverter 'Connect' 'cable|line|wire'
    if ($cmd) { $addin.Command($cmd) | Out-Null; Start-Sleep 3 }
    $all = @(Units).Count
    Write-IskInfo ("PV units: $all (" + ((Units | ForEach-Object { $_.Name }) -join ', ') + ')')
    $S.Slide.Shapes.Item($cable1).Select(); Start-Sleep 3
    $ids = Get-CommandIds
    Assert-Isk $S ($ids -contains 'connect:net') 'a cable offers "Select connected > Net"'
    $addin.Command('connect:net') | Out-Null; Start-Sleep 3
    Write-IskInfo ('selected: ' + (Selected) + ' of ' + $all + '; ' + (Get-LogTail 'Select connected \('))
    Assert-Isk $S ($all -ge 3 -and (Selected) -eq $all) "Net selects the PV string, its cables and the inverter ($(Selected) of $all)"
    $halo = Get-LogTail 'Halo: '
    Assert-Isk $S ($halo -match [regex]::Escape($pv)) 'the halo covers the whole net'
    Save-IskShot $S '1-net'
}

# 2) A gear with a shaft through its hub, a second gear on the shaft and a meshing partner: "Moves along" selects all four.
Clear-Slide
$g = $addin.Insert('mech.spur.stick'); $gs = $S.Slide.Shapes.Item($g); $gs.Left = 200; $gs.Top = 150
$gs.Select(); Start-Sleep 2
$cmd = Get-PortCommand $g 'Hub' 'shaft'
Assert-Isk $S ($null -ne $cmd) 'a free gear hub offers a shaft'
if ($cmd) {
    $addin.Command($cmd) | Out-Null; Start-Sleep 3
    $shaft = SelectedName
    $cmd = Get-PortCommand $shaft 'AxisEnd' 'mech\.(spur|gear)'
    if ($cmd) { $addin.Command($cmd) | Out-Null; Start-Sleep 3 }
    $S.Slide.Shapes.Item($g).Select(); Start-Sleep 2
    $cmd = Get-PortCommand $g 'Mesh' 'mech\.(spur|gear)'
    if ($cmd) { $addin.Command($cmd) | Out-Null; Start-Sleep 3 }
    $all = @(Units).Count
    Write-IskInfo ("drive units: $all (" + ((Units | ForEach-Object { $_.Name }) -join ', ') + ')')
    $S.Slide.Shapes.Item($g).Select(); Start-Sleep 3
    $ids = Get-CommandIds
    Assert-Isk $S ($ids -contains 'connect:coupled') 'a coupled gear offers "Select connected > Moves along"'
    $addin.Command('connect:coupled') | Out-Null; Start-Sleep 3
    Write-IskInfo ('selected: ' + (Selected) + ' of ' + $all + '; ' + (Get-LogTail 'Select connected \('))
    Assert-Isk $S ($all -ge 3 -and (Selected) -eq $all) "Moves along selects the shaft and every gear ($(Selected) of $all)"
    # "Select the assembly" is the same.
    $S.Slide.Shapes.Item($g).Select(); Start-Sleep 2
    $addin.Command('assembly') | Out-Null; Start-Sleep 3
    Assert-Isk $S ((Selected) -eq $all) "Select the assembly selects the same ($(Selected))"
    Save-IskShot $S '2-moves-along'
}

# 3) Three flowchart steps by quick add: "Following elements" from the first selects the rest and the connectors;
#    Ctrl+double-click on the first step does the same.
Clear-Slide
$first = $addin.Insert('ann.flow.process'); $fs = $S.Slide.Shapes.Item($first); $fs.Left = 300; $fs.Top = 40
$fs.Select(); Start-Sleep 2
for ($i = 0; $i -lt 2; $i++) {
    $cur = SelectedName
    $r = Get-IskRect $S $cur
    Invoke-IskClick $r.CX ($r.Y + $r.H + 13 * $k) -WaitMs 3000
}
$all = @(Units).Count
Write-IskInfo ("flow units: $all (" + ((Units | ForEach-Object { $_.Name }) -join ', ') + ')')
Assert-Isk $S ($all -ge 3) "three steps were added by quick add ($all units)"
$S.Window.Selection.Unselect(); Start-Sleep 1
$S.Slide.Shapes.Item($first).Select(); Start-Sleep 3
$addin.Command('connect:chain') | Out-Null; Start-Sleep 3
Write-IskInfo ('selected: ' + (Selected) + ' of ' + $all + '; ' + (Get-LogTail 'Select connected \('))
Assert-Isk $S ((Selected) -eq $all) "Following elements selects the next steps and their connectors ($(Selected) of $all)"
Save-IskShot $S '3-following'
$S.Window.Selection.Unselect(); Start-Sleep 1
$S.Slide.Shapes.Item($first).Select(); Start-Sleep 3
$r = Get-IskRect $S $first
Move-IskMouse $r.CX $r.CY; Start-Sleep -Milliseconds 500
[IskTest.Native]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80
foreach ($n in 1..2) {
    [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 40
    [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
}
[IskTest.Native]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero); Start-Sleep 3
Write-IskInfo ('Ctrl+double-click: ' + (Get-LogTail 'Ctrl\+double-click|Select connected \('))
Assert-Isk $S ((Selected) -eq $all) "Ctrl+double-click selects the following elements ($(Selected) of $all)"

# 4) Two overlapping boxes: the halo names only the selected one; its outline shows through the box on top.
Clear-Slide
$a = $addin.Insert('ann.flow.process'); $as = $S.Slide.Shapes.Item($a); $as.Left = 300; $as.Top = 200
$b = $addin.Insert('ann.flow.process'); $bs = $S.Slide.Shapes.Item($b); $bs.Left = $as.Left + $as.Width * 0.4; $bs.Top = $as.Top + $as.Height * 0.4
$S.Window.Selection.Unselect(); Start-Sleep 1
$as.Select(); Start-Sleep 3
$halo = Get-LogTail 'Halo: '
Write-IskInfo ('halo: ' + $halo)
$sel = if ($halo -match 'selected=([^;]*);') { $Matches[1] } else { '' }
Assert-Isk $S (($sel -match [regex]::Escape($a)) -and -not ($sel -match [regex]::Escape($b))) 'the halo names the selected box only'
$halo2 = try { [string]$addin.Halo() } catch { '' }
if ($halo2) { Write-IskInfo ('Halo() = ' + $halo2) }
$r = Get-IskRect $S $a
Save-IskShot $S '4-overlap-halo' -Around ($r.X - 80), ($r.Y - 80), ($r.W * 2 + 160), ($r.H * 2 + 160)
# Both selected: both glow.
$S.Slide.Shapes.Range([string[]]@($a, $b)).Select(); Start-Sleep 3
$halo = Get-LogTail 'Halo: '
Write-IskInfo ('halo (both): ' + $halo)
Assert-Isk $S (($halo -match [regex]::Escape($a)) -and ($halo -match [regex]::Escape($b))) 'a multi-selection: both boxes glow'
Save-IskShot $S '5-both-halo' -Around ($r.X - 80), ($r.Y - 80), ($r.W * 2 + 160), ($r.H * 2 + 160)
$S.Window.Selection.Unselect()
