# Scenario (UX review 2026-11-25, F2): the slide-wide symbol size scales every standard symbol of the slide uniformly
# (x1.5), leaves larger elements alone, and new symbols take it.
param($S)
$addin = $S.AddIn
$S.Window.Selection.Unselect()
$v = $addin.Insert('fluid.check'); $vs = $S.Slide.Shapes.Item($v)
$t = $addin.Insert('ann.table'); $ts = $S.Slide.Shapes.Item($t)
$w0 = $vs.Width; $tw0 = $ts.Width
Write-IskInfo ("check valve {0:N1} pt, table {1:N1} pt" -f $w0, $tw0)
Assert-Isk $S ($addin.SetSymbolSize(1.5)) 'the symbol size can be set'
Start-Sleep 2
# Redrawn units keep their names.
$vs = @(Get-IskShapes $S) | Where-Object { $_.Name -eq $v } | Select-Object -First 1
$ts = @(Get-IskShapes $S) | Where-Object { $_.Name -eq $t } | Select-Object -First 1
Write-IskInfo ("after x1.5: check valve {0:N1} pt, table {1:N1} pt" -f $vs.Width, $ts.Width)
Assert-Isk $S ([math]::Abs($vs.Width / $w0 - 1.5) -lt 0.08) 'the symbol grew by 1.5'
Assert-Isk $S ([math]::Abs($ts.Width - $tw0) -lt 1) 'the table kept its size'
$S.Window.Selection.Unselect()
$n = $addin.Insert('fluid.check'); $ns = $S.Slide.Shapes.Item($n)
Write-IskInfo ("new symbol {0:N1} pt" -f $ns.Width)
Assert-Isk $S ([math]::Abs($ns.Width / $w0 - 1.5) -lt 0.08) 'a new symbol takes the slide size'
Save-IskShot $S '1-symbols-x1.5'
