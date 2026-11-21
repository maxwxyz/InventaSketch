# Scenario: the floating bar for an arrow, an e-machine and a gear in a gear train; the bar never covers the drawing.
param($S)
$addin = $S.AddIn

function Test-Overlap($a, $b) { $a.X -lt $b.X + $b.W -and $b.X -lt $a.X + $a.W -and $a.Y -lt $b.Y + $b.H -and $b.Y -lt $a.Y + $a.H }

function Show-Bar([string]$shot) {
    $bar = Get-IskBarRect $S
    Assert-Isk $S ($null -ne $bar) "the bar is shown ($shot)"
    if (-not $bar) { return }
    Write-IskInfo "bar $($bar.X),$($bar.Y) $($bar.W)x$($bar.H)"
    Save-IskShot $S $shot -Around ($bar.X - 20), ($bar.Y - 20), ($bar.W + 40), ($bar.H + 40)
    $hit = Get-IskShapes $S | Where-Object { Test-Overlap $bar (Get-IskRect $S $_.Name) }
    Assert-Isk $S (-not $hit) "the bar covers no sketch shape ($shot)"
}

$arrow = $addin.Insert('ann.arrow')
$S.Slide.Shapes.Item($arrow).Select(); Start-Sleep 3
Show-Bar '1-arrow-bar'

$S.Window.Selection.Unselect()
$em = $addin.Insert('machines.emachine.stick')
$e = $S.Slide.Shapes.Item($em); $e.Left = 150; $e.Top = 300
$e.Select(); Start-Sleep 3
Show-Bar '2-emachine-bar'

# A shaft with a gear on it and a meshing gear: the bar avoids all three.
$S.Window.Selection.Unselect()
$shaft = $addin.Insert('mech.shaft.line')
$S.Slide.Shapes.Item($shaft).Left = 500
$S.Slide.Shapes.Item($shaft).Select(); Start-Sleep 2
$addin.Command('ins:mech.spur.stick') | Out-Null; Start-Sleep 4
$addin.Command('ins:mech.spur.stick') | Out-Null; Start-Sleep 4
Write-IskShapes $S
Show-Bar '3-train-bar'
Save-IskShot $S '4-slide'
