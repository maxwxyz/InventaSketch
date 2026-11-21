# Scenario: predefined linkages explode into self-built ones; the exploded quick-return keeps its pin in the slot when dragged.
param($S)
$addin = $S.AddIn

$expected = [ordered]@{
    'kin.fourbar'     = @(40, 60, 'ISK:kin.link*', 3)
    'kin.slidercrank' = @(260, 60, 'ISK:kin.link*', 2)
    'kin.toggle'      = @(80, 280, 'ISK:kin.link*', 2)
    'kin.quickreturn' = @(330, 250, 'ISK:kin.link*', 3)
    'kin.scotchyoke'  = @(620, 300, 'ISK:kin.link.ternary*', 1)
}
foreach ($item in $expected.Keys) {
    $e = $expected[$item]
    $countBefore = @(Get-IskShapes $S $e[2]).Count
    $n = $addin.Insert($item)
    $shape = $S.Slide.Shapes.Item($n); $shape.Left = $e[0]; $shape.Top = $e[1]; $shape.Select(); Start-Sleep 3
    $ok = $addin.Command('explode'); Start-Sleep 6
    $S.Window.Selection.Unselect()
    Assert-Isk $S ($ok -and @(Get-IskShapes $S $e[2]).Count - $countBefore -eq $e[3]) "$item explodes into its parts"
}
Write-IskShapes $S
Save-IskShot $S '1-exploded'

# Quick-return: the crank is the leftmost link docked on a slot; its pin slides in the lever's slot.
$slotted = @(Get-IskShapes $S 'ISK:kin.link*' | Where-Object { (Get-IskModel $_) -match 'slot0' } | Sort-Object Left)
$crank = $slotted | Where-Object { $_.Left -gt 250 } | Select-Object -First 1
$leverId = [regex]::Match((Get-IskModel $crank), '"objectId":\s*"([^"]+)"[^}]*"anchorId":\s*"slot0"').Groups[1].Value
function Find-Lever { Get-IskShapes $S 'ISK:kin.link*' | Where-Object { (Get-IskModel $_) -match ('"id":\s*"' + $leverId + '"') -and (Get-IskModel $_) -notmatch 'slot0' } | Select-Object -First 1 }
$c0 = Get-IskPoints $crank; $l0 = Get-IskPoints (Find-Lever)
Write-IskInfo ('before: crank {0} | lever {1}' -f (Format-IskPoints $c0), (Format-IskPoints $l0))
$crank.Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('motion')) 'motion mode switches on'
Start-Sleep 2
$crank.Select(); Start-Sleep 3
$pv = ConvertTo-IskPixel $S $c0[0]; $tp = ConvertTo-IskPixel $S $c0[1]
Invoke-IskArcDrag -Center $pv -Radius (Get-IskDistance $pv $tp) -Start ([Math]::Atan2($tp[1] - $pv[1], $tp[0] - $pv[0])) -Sweep 1.0 -BeforeRelease { Save-IskShot $S '2-dragging' -Around ($pv[0] - 450), ($pv[1] - 500), 900, 800 }
Start-Sleep 2
# Redrawn shapes get new names: find them again by their docking.
$crank = @(Get-IskShapes $S 'ISK:kin.link*' | Where-Object { (Get-IskModel $_) -match 'slot0' -and $_.Left -gt 250 } | Sort-Object Left)[0]
$c1 = Get-IskPoints $crank; $l1 = Get-IskPoints (Find-Lever)
Write-IskInfo ('after:  crank {0} | lever {1}' -f (Format-IskPoints $c1), (Format-IskPoints $l1))
Assert-Isk $S ((Get-IskDistance $c0[1] $c1[1]) -gt 1) ('the crank turned (pin moved {0:0.00} mm)' -f (Get-IskDistance $c0[1] $c1[1]))
Assert-Isk $S ([Math]::Abs((Get-IskDistance $c0[0] $c0[1]) - (Get-IskDistance $c1[0] $c1[1])) -lt 0.01) 'the crank keeps its length'
Assert-Isk $S ([Math]::Abs((Get-IskDistance $l0[0] $l0[1]) - (Get-IskDistance $l1[0] $l1[1])) -lt 0.01) 'the slotted lever keeps its length'
Assert-Isk $S ((Get-IskLineDistance $c1[1] $l1[0] $l1[1]) -lt 0.01) ('the pin stays in the slot ({0:0.0000} mm off)' -f (Get-IskLineDistance $c1[1] $l1[0] $l1[1]))
$addin.Command('motion') | Out-Null
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '3-after' -Around ($pv[0] - 450), ($pv[1] - 500), 900, 800
