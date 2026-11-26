# Diagnosis (UX review 2026-11-25): where do the shaft and the fitted parts land?
param($S)
$addin = $S.AddIn
function Info($label, $sh) {
    if (-not $sh) { Write-IskInfo "$label : none"; return }
    $m = Get-IskModel $sh
    $t = [regex]::Match($m, '"type"\s*:\s*"([^"]+)"').Groups[1].Value
    Write-IskInfo ("{0}: {1} type={2} at {3:N0},{4:N0} size {5:N0}x{6:N0}" -f $label, $sh.Name, $t, $sh.Left, $sh.Top, $sh.Width, $sh.Height)
}
$S.Window.Selection.Unselect()
$n = $addin.Insert('mech.shaft.body'); Write-IskInfo ("Insert returned '" + $n + "'")
$sh = $S.Slide.Shapes.Item($n); Info 'shaft after insert' $sh
# Each move may redraw the unit (new shape): look the shaft up again.
function Shaft { @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'mech\.shaft' } | Select-Object -First 1 }
$sh.Left = 250; Start-Sleep 1; $sh = Shaft; $sh.Top = 230; Start-Sleep 1; $sh = Shaft; Info 'shaft moved' $sh
$sh.Select(); Start-Sleep 2
Write-IskInfo ("bar: " + $addin.BarIds())
$addin.Command('ins:mech.spur.side') | Out-Null; Start-Sleep 3
Info 'selected after ins gear' (Get-IskSelected $S)
foreach ($x in @(Get-IskShapes $S)) { Info '  unit' $x }
Save-IskShot $S '1-after-gear'
$shaft = @(Get-IskShapes $S) | Where-Object { (Get-IskModel $_) -match 'mech\.shaft' } | Select-Object -First 1
$shaft.Select(); Start-Sleep 2
$addin.Command('ins:mech.bearing.ball.stick') | Out-Null; Start-Sleep 3
Info 'selected after ins bearing' (Get-IskSelected $S)
foreach ($x in @(Get-IskShapes $S)) { Info '  unit' $x }
Save-IskShot $S '2-after-bearing'
