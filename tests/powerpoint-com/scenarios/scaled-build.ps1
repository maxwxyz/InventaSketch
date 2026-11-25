# Scenario: "+" on scaled elements (maintainer report 2026-11-25). A road made 1.5 times wider with PowerPoint's own
# handles gets a next segment of the same width that continues it seamlessly; a gear scaled 1.5 times gets a meshing gear
# of the same scale. Road sizes fit the slide (top views at 0.6 of their design size).
param($S)
$addin = $S.AddIn

$name = $addin.Insert('auto.road.2'); $road = $S.Slide.Shapes.Item($name)
$road.Left = 20; $road.Top = 150
$S.Window.Selection.Unselect(); Start-Sleep 1
$hMm0 = $road.Height / 72 * 25.4
Write-IskInfo ("road inserted: {0:N1} x {1:N1} mm" -f ($road.Width / 72 * 25.4), $hMm0)
Assert-Isk $S ($road.Width / 72 * 25.4 -lt 140 -and $hMm0 -lt 60) 'the road fits the slide'
$road.LockAspectRatio = 0
$road.Height = $road.Height * 1.5
Start-Sleep 2
$road.Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('port:axis0.end:auto.road.2')) 'the "+" at the lane end takes a road'
Start-Sleep 4
$roads = @(Get-IskShapes $S 'ISK:auto.road*' | Sort-Object Left)
Write-IskShapes $S
Assert-Isk $S ($roads.Count -eq 2) "two road units ($($roads.Count))"
if ($roads.Count -eq 2) {
    $a = $roads[0]; $b = $roads[1]
    Write-IskInfo ("first {0:N1}..{1:N1} pt, h {2:N1}; second {3:N1}..{4:N1} pt, h {5:N1}" -f $a.Left, ($a.Left + $a.Width), $a.Height, $b.Left, ($b.Left + $b.Width), $b.Height)
    Assert-Isk $S ([math]::Abs($a.Height - $b.Height) -lt 1.5) 'the new segment is as wide as the scaled road'
    Assert-Isk $S ([math]::Abs($a.Top - $b.Top) -lt 1.5) 'lane for lane (same top edge)'
    Assert-Isk $S ([math]::Abs(($a.Left + $a.Width) - $b.Left) -lt 1.5) 'it continues without a gap'
}
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '1-road'

$gname = $addin.Insert('mech.spur.axial'); $gear = $S.Slide.Shapes.Item($gname)
$gear.Left = 120; $gear.Top = 20
$w0 = $gear.Width
$gear.LockAspectRatio = -1
$gear.Width = $gear.Width * 1.5
Start-Sleep 2
$gear.Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('port:mesh.right:mech.spur.axial')) 'a gear meshes on the scaled gear'
Start-Sleep 4
$gears = @(Get-IskShapes $S 'ISK:mech.gear*' | Sort-Object Left)
Assert-Isk $S ($gears.Count -eq 2) "two gear units ($($gears.Count))"
if ($gears.Count -eq 2) {
    Write-IskInfo ("gear widths {0:N1} / {1:N1} pt (inserted {2:N1})" -f $gears[0].Width, $gears[1].Width, $w0)
    Assert-Isk $S ([math]::Abs($gears[1].Width - $gears[0].Width) -lt 2) 'the meshing gear has the same scale'
}
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '2-gear'
