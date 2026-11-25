# Scenario: clicks on a placed efficiency map without dragging (maintainer report 2026-11-25: "a click on an efficiency map
# starts moving, but it cannot be placed any more"): two clicks, then the pointer moves with no button pressed and clicks
# elsewhere; the chart must stay where it is and nothing may hang on the pointer.
param($S)
$addin = $S.AddIn

$name = $addin.Insert('chart.efficiency'); $shape = $S.Slide.Shapes.Item($name)
$shape.Left = 120; $shape.Top = 90
$S.Window.Selection.Unselect(); Start-Sleep 2
$left0 = $shape.Left; $top0 = $shape.Top
$r = Get-IskRect $S $name
$c = @(($r.X + $r.W * 0.5), ($r.Y + $r.H * 0.5))
Invoke-IskClick $c[0] $c[1] 2000            # selects
Invoke-IskClick $c[0] $c[1] 2000            # click on the selected chart
Move-IskMouse ($c[0] + 150) ($c[1] + 80); Start-Sleep -Milliseconds 800
Move-IskMouse ($c[0] + 260) ($c[1] + 120); Start-Sleep -Milliseconds 1200
Save-IskShot $S '1-pointer-moved'
$s1 = $S.Slide.Shapes.Item($name)
Write-IskInfo ("after hover: {0:N1} / {1:N1} pt (start {2:N1} / {3:N1})" -f $s1.Left, $s1.Top, $left0, $top0)
Assert-Isk $S ([math]::Abs($s1.Left - $left0) -lt 1 -and [math]::Abs($s1.Top - $top0) -lt 1) 'the chart does not follow the pointer without a pressed button'
Invoke-IskClick ($c[0] + 260) ($c[1] + 120) 2500
$s2 = $S.Slide.Shapes.Item($name)
Write-IskInfo ("after a click elsewhere: {0:N1} / {1:N1} pt" -f $s2.Left, $s2.Top)
Save-IskShot $S '2-clicked-elsewhere'
Assert-Isk $S ([math]::Abs($s2.Left - $left0) -lt 1 -and [math]::Abs($s2.Top - $top0) -lt 1) 'a click elsewhere does not move it'
# A double-click on the chart: must not leave a move pending either.
$r = Get-IskRect $S $name
$c = @(($r.X + $r.W * 0.5), ($r.Y + $r.H * 0.5))
Invoke-IskClick $c[0] $c[1] 1500
Move-IskMouse $c[0] $c[1]; [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80; [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep 2
Move-IskMouse ($c[0] + 200) ($c[1] + 100); Start-Sleep 1
Save-IskShot $S '3-after-double-click'
$s3 = $S.Slide.Shapes.Item($name)
Assert-Isk $S ([math]::Abs($s3.Left - $left0) -lt 1 -and [math]::Abs($s3.Top - $top0) -lt 1) 'a double-click leaves nothing pending'
Send-IskEscape; Start-Sleep 1
