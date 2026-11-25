# Scenario (plan review 2026-11-25, item 2): "Add ray bundle" on a light source inserts the ray as a new unit docked on the
# source (one undo step); "Remove ray bundle" takes it away again. Document actions that create or remove objects outside
# the selected unit are applied on the slide.
param($S)
$addin = $S.AddIn

function RayUnits {
    @(@(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'optics\.ray' })
}

$S.Window.Selection.Unselect()
function Source { @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'optics\.source\.laser' } | Select-Object -First 1 }
$addin.Insert('optics.source.laser') | Out-Null; Start-Sleep 1
$src = Source
$n0 = @(RayUnits).Count
Write-IskInfo ("ray units at start: $n0")
$src = Source; $src.Select(); Start-Sleep 2
$bar = $addin.BarIds()
Write-IskInfo ("bar: $bar")
$addin.Command('act:optics.trace') | Out-Null; Start-Sleep 3
$rays = @(RayUnits)
Write-IskInfo ("ray units after add: $($rays.Count)")
Assert-Isk $S ($rays.Count -eq $n0 + 1) 'Add ray bundle inserts one ray unit'
if ($rays.Count -gt 0) {
    $ray = $rays[$rays.Count - 1]
    $docked = (Get-IskModel $ray) -match '"attach"'
    Assert-Isk $S $docked 'the new ray is docked on the source'
    # The source unit is redrawn by the action: look it up again.
    $src2 = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'optics\.source\.laser' } | Select-Object -First 1
    Write-IskInfo ("source right {0:N0} pt, ray left {1:N0} pt" -f ($src2.Left + $src2.Width), $ray.Left)
    Assert-Isk $S ([math]::Abs($ray.Left - ($src2.Left + $src2.Width)) -lt 20) 'the ray starts at the source'
}
Save-IskShot $S '1-ray-added'

$src = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'optics\.source\.laser' } | Select-Object -First 1
$src.Select(); Start-Sleep 2
$addin.Command('act:optics.trace') | Out-Null; Start-Sleep 3
Write-IskInfo ("ray units after remove: $(@(RayUnits).Count)")
Assert-Isk $S (@(RayUnits).Count -eq $n0) 'Remove ray bundle removes the ray unit'
Save-IskShot $S '2-ray-removed'
