# Scenario: build ports ("+"): dock a meshing gear, a bearing at a shaft end, stretch a shaft by dragging its end "+".
param($S)
$addin = $S.AddIn
$k = Get-IskScale

$g1 = $addin.Insert('mech.spur.axial')
$s1 = $S.Slide.Shapes.Item($g1); $s1.Left = 200; $s1.Top = 150
$s1.Select(); Start-Sleep 3
Save-IskShot $S '1-gear-ports'
Assert-Isk $S ($addin.Command('port:mesh.right:mech.spur.axial')) 'the mesh port on the right is offered'
Start-Sleep 4
Assert-Isk $S (@(Get-IskShapes $S 'ISK:mech.gear.spur*').Count -eq 2) 'the port docks a meshing gear'

$S.Window.Selection.Unselect()
$sh = $addin.Insert('mech.shaft.line')
$s2 = $S.Slide.Shapes.Item($sh); $s2.Left = 250; $s2.Top = 400
$s2.Select(); Start-Sleep 3
$items = $addin.LibraryItems() -split "`n"
$bearing = $items | Where-Object { $_ -like 'mech.bearing.ball*' -and $_ -like '*stick*' } | Select-Object -First 1
if (-not $bearing) { $bearing = $items | Where-Object { $_ -like 'mech.bearing.ball*' } | Select-Object -First 1 }
Assert-Isk $S ($addin.Command("port:axis0.end:$bearing")) "a bearing ($bearing) docks at the shaft end"
Start-Sleep 4
Write-IskShapes $S

$shaft = @(Get-IskShapes $S 'ISK:mech.shaft*')[0]
$shaft.Select(); Start-Sleep 3
$width = $shaft.Width
$r = Get-IskRect $S $shaft.Name
Save-IskShot $S '2-shaft-ports'
# The "+" of the left end sits 15 logical px left of it.
Invoke-IskDrag -From @(($r.X - 15 * $k), $r.CY) -To @(($r.X - 15 * $k - 120), $r.CY)
$shaft = @(Get-IskShapes $S 'ISK:mech.shaft*')[0]
Assert-Isk $S ($shaft.Width -gt $width + 10) ('dragging the end "+" stretches the shaft ({0:0.0} -> {1:0.0} pt)' -f $width, $shaft.Width)
Save-IskShot $S '3-shaft-stretched'
