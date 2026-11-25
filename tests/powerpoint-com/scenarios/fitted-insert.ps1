# Scenario (maintainer 2026-11-25): inserting a fitting part. (1) A gear on a shaft is selected and "Insert ..." picks a
# ball bearing: it goes onto the gear's shaft (the selected unit is not the shaft). (2) Insert at the pointer: while the
# bearing follows the pointer near the shaft, the ghost already shows it fitted on the shaft (screenshot); the click
# inserts it there, centred on the shaft axis.
param($S)
$addin = $S.AddIn
$k = Get-IskScale

$bearing = @(($addin.LibraryItems() -split "`n") | Where-Object { $_ -match '^mech\.bearing\.ball' }) | Select-Object -First 1
Assert-Isk $S ($null -ne $bearing) "a ball bearing is in the library ($bearing)"
if (-not $bearing) { return }

function Get-Shaft { Get-IskShapes $S 'ISK:mech.shaft*' | Select-Object -First 1 }
function Get-Bearings { @(Get-IskShapes $S 'ISK:mech.bearing*') }
function Test-OnShaft([string]$what) {
    $sh = Get-Shaft; $b = Get-Bearings | Select-Object -First 1
    if (-not $b -or -not $sh) { Assert-Isk $S $false "$what`: bearing and shaft exist"; return }
    $axis = $sh.Top + $sh.Height / 2
    $centre = $b.Top + $b.Height / 2
    Write-IskInfo ("{0}: bearing centre y {1:0.0} pt, shaft axis y {2:0.0} pt, bearing {3:0.0} x {4:0.0} pt" -f $what, $centre, $axis, $b.Width, $b.Height)
    Assert-Isk $S ([Math]::Abs($centre - $axis) -lt 1.5) "$what`: the bearing sits on the shaft axis"
}

# A horizontal shaft with a gear on it.
$shaft = $addin.Insert('mech.shaft.line'); $S.Slide.Shapes.Item($shaft).Left = 180; $S.Slide.Shapes.Item($shaft).Top = 250
$S.Slide.Shapes.Item($shaft).Select(); Start-Sleep 2
$addin.Command('ins:mech.spur.stick') | Out-Null; Start-Sleep 4
$gear = Get-IskShapes $S 'ISK:mech.gear*' | Select-Object -First 1
Assert-Isk $S ($null -ne $gear) 'a gear sits on the shaft'

# 1) The gear selected: the bearing goes onto its shaft.
$gear.Select(); Start-Sleep 3
$addin.Command("ins:$bearing") | Out-Null; Start-Sleep 4
Write-IskShapes $S
Assert-Isk $S ((Get-Bearings).Count -eq 1) 'with the gear selected, the bearing is inserted (onto its shaft)'
Test-OnShaft 'from the gear'
Save-IskShot $S '1-bearing-from-gear'

# 2) Insert at the pointer with the fitted preview.
foreach ($b in Get-Bearings) { $b.Delete() }
$S.Window.Selection.Unselect(); Start-Sleep 1
$r = Get-IskRect $S (Get-Shaft).Name
Move-IskMouse ($r.X + $r.W * 0.75) ($r.CY - 120 * $k); Start-Sleep -Milliseconds 500
Assert-Isk $S ($addin.Place($bearing)) 'insert at the pointer starts'
Start-Sleep 1
# Near the shaft (a little above the axis): the ghost shows the bearing fitted on the shaft.
Move-IskMouse ($r.X + $r.W * 0.75) ($r.CY - 25 * $k); Start-Sleep -Milliseconds 400
Move-IskMouse ($r.X + $r.W * 0.75 + 2) ($r.CY - 22 * $k); Start-Sleep -Milliseconds 900
Save-IskShot $S '2-fitted-ghost' -Around ($r.X - 60), ($r.CY - 200), ($r.W + 120), 400
Invoke-IskClick ($r.X + $r.W * 0.75 + 2) ($r.CY - 22 * $k) -WaitMs 4000
Write-IskShapes $S
Assert-Isk $S ((Get-Bearings).Count -eq 1) 'the click inserts the bearing'
Test-OnShaft 'at the pointer'
Save-IskShot $S '3-bearing-at-pointer'
Send-IskEscape; Start-Sleep 1
