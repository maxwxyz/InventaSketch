# Scenario (optics, interactive light rays): a laser with its ray bundle and a lens; dragging the lens shows the traced ray
# following live (second ghost colour) before the release; after the release the ray is re-traced through the moved lens.
param($S)
$addin = $S.AddIn
function Find($pattern) { @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match $pattern } | Select-Object -First 1 }

$S.Window.Selection.Unselect()
$addin.Insert('optics.source.laser') | Out-Null; Start-Sleep 1
$src = Find 'optics\.source\.laser'; $src.Left = 120; Start-Sleep 1
$src = Find 'optics\.source\.laser'; $src.Top = 220; Start-Sleep 1
$src = Find 'optics\.source\.laser'; $src.Select(); Start-Sleep 2
$addin.Command('act:optics.trace') | Out-Null; Start-Sleep 3
$S.Window.Selection.Unselect()
$addin.Insert('optics.lens.biconvex') | Out-Null; Start-Sleep 1
# The lens in the ray's path, a little above the axis (the ray passes below its centre and is bent).
$src = Find 'optics\.source\.laser'
$axisY = $src.Top + $src.Height / 2
$x = $src.Left + $src.Width + 50
$lens = Find 'optics\.lens'; $lens.Left = $x; Start-Sleep 1
$lens = Find 'optics\.lens'; $lens.Top = $axisY - $lens.Height / 2 - 12; Start-Sleep 2
$lens = Find 'optics\.lens'
Write-IskInfo ("laser {0:N0},{1:N0} {2:N0}x{3:N0}; lens {4:N0},{5:N0} {6:N0}x{7:N0}" -f $src.Left, $src.Top, $src.Width, $src.Height, $lens.Left, $lens.Top, $lens.Width, $lens.Height)
$ray0 = Find '"optics\.ray"'
$b0 = '{0:N0},{1:N0},{2:N0},{3:N0}' -f $ray0.Left, $ray0.Top, $ray0.Width, $ray0.Height
Save-IskShot $S '1-before'

$lens = Find 'optics\.lens'; $lens.Select(); Start-Sleep 3
$r = Get-IskRect $S $lens.Name
Invoke-IskDrag -From @($r.CX, $r.CY) -To @($r.CX, ($r.CY + 70)) -BeforeRelease { Save-IskShot $S '2-dragging' }
Start-Sleep 3
$log = Get-Content (Get-IskLog $S)
$live = $log | Where-Object { $_ -match 'Live dependents: ' } | Select-Object -Last 1
Write-IskInfo ("log: " + $live)
Assert-Isk $S ($null -ne $live -and $live -match 'optics\.ray') 'the ray follows live while the lens is dragged'
$ray1 = Find '"optics\.ray"'
$b1 = '{0:N0},{1:N0},{2:N0},{3:N0}' -f $ray1.Left, $ray1.Top, $ray1.Width, $ray1.Height
Write-IskInfo ("ray bounds $b0 -> $b1")
Assert-Isk $S ($b1 -ne $b0) 'after the release the ray is traced again through the moved lens'
Save-IskShot $S '3-after'
