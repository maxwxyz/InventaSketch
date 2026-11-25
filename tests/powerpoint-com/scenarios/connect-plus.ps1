# Scenario (maintainer report 2026-11-25: "with PV, building services etc. you cannot connect further lines/cables with
# '+' and keep building"): a PV string offers its connection ports; "+" there places the DC cable docked on the port; the
# cable's free end offers "+" again and an inverter docks onto it. Every step is one unit per element, docked.
param($S)
$addin = $S.AddIn

function Get-Ports($name) { @(($addin.BuildPorts($name) -split "`n") | Where-Object { $_ }) }
function Units { @(Get-IskShapes $S) }

$S.Window.Selection.Unselect()
$pv = $addin.Insert('energy.pv.string.10'); $shape = $S.Slide.Shapes.Item($pv)
$shape.Left = 80; $shape.Top = 120
$shape.Select(); Start-Sleep 2
$ports = Get-Ports $pv
Write-IskInfo ("PV ports: " + ($ports -join ' ; '))
$connect = $ports | Where-Object { ($_ -split '\|')[1] -eq 'Connect' } | Select-Object -First 1
Assert-Isk $S ($null -ne $connect) 'the PV string offers a connection "+"'
if (-not $connect) { return }
$cid = ($connect -split '\|')[0]; $items = (($connect -split '\|')[2] -split ',')
Assert-Isk $S ($items.Count -ge 1 -and $items[0] -match 'cable|line|wire') "the first offer is a line ($($items[0]))"
$before = @(Units).Count
$addin.Command("port:${cid}:$($items[0])") | Out-Null; Start-Sleep 3
$after = @(Units)
Write-IskInfo ("units: {0} -> {1}" -f $before, $after.Count)
Assert-Isk $S ($after.Count -eq $before + 1) 'the cable is placed as a unit of its own'
Save-IskShot $S '1-cable'

# The new cable is selected; its free end offers "+" again.
$sel = try { $S.Window.Selection.ShapeRange.Item(1).Name } catch { '' }
$linePorts = Get-Ports $sel
Write-IskInfo ("cable ports: " + ($linePorts -join ' ; '))
$end = $linePorts | Where-Object { ($_ -split '\|')[1] -eq 'LineEnd' } | Select-Object -First 1
Assert-Isk $S ($null -ne $end) 'the free cable end offers "+"'
if (-not $end) { return }
$eid = ($end -split '\|')[0]; $offers = (($end -split '\|')[2] -split ',')
$inverter = $offers | Where-Object { $_ -match 'inverter' } | Select-Object -First 1
Assert-Isk $S ($null -ne $inverter) "an inverter is offered at the cable end ($($offers -join ','))"
if ($inverter) {
    $addin.Command("port:${eid}:$inverter") | Out-Null; Start-Sleep 3
    $final = @(Units)
    Assert-Isk $S ($final.Count -eq $before + 2) 'the inverter is placed and docked at the cable end'
    $models = ($final | ForEach-Object { Get-IskModel $_ }) -join ' '
    Assert-Isk $S ($models -match 'energy.conv.inverter') 'the inverter is on the slide'
}
Save-IskShot $S '2-inverter'
Send-IskEscape; Start-Sleep 1
