# Scenario: placing charts from the ribbon gallery (Automation.Place = a click on the gallery item): the element follows
# the pointer and a real click on the slide drops it (maintainer report 2026-11-25: an efficiency map could not be placed).
param($S)
$addin = $S.AddIn

$i = 0
foreach ($item in 'chart.efficiency', 'chart.line', 'chart.torque', 'mech.spur.axial') {
    $S.Window.Selection.Unselect(); Start-Sleep 1
    $before = @(Get-IskShapes $S 'ISK:*').Count
    Assert-Isk $S ($addin.Place($item)) "$item starts placing"
    $pt = ConvertTo-IskPixel $S @((60 + 60 * $i), 90)
    Move-IskMouse ($pt[0] - 40) ($pt[1] - 20); Start-Sleep -Milliseconds 600
    Move-IskMouse $pt[0] $pt[1]; Start-Sleep -Milliseconds 800
    Save-IskShot $S "$item-1-following"
    Invoke-IskClick $pt[0] $pt[1] 3000
    $after = @(Get-IskShapes $S 'ISK:*').Count
    Save-IskShot $S "$item-2-placed"
    Assert-Isk $S ($after -eq $before + 1) "${item}: a click on the slide places it ($before -> $after)"
    Send-IskEscape; Start-Sleep 1
    $i++
}
