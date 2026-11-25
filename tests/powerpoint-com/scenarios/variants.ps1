# Scenario: variants – a slide is duplicated (the copy keeps the elements' ids), on the copy one gear is removed, one
# changed and one added; "Compare with the previous slide" marks them (3 marks + legend), called again it removes them.
param($S)
$addin = $S.AddIn

$a = $addin.Insert('mech.spur.axial'); $sa = $S.Slide.Shapes.Item($a); $sa.Left = 80; $sa.Top = 120
$S.Window.Selection.Unselect()
$b = $addin.Insert('mech.spur.axial'); $sb = $S.Slide.Shapes.Item($b); $sb.Left = 330; $sb.Top = 120
$S.Window.Selection.Unselect(); Start-Sleep 1
$copy = $S.Slide.Duplicate().Item(1)
$S.Window.View.GotoSlide($copy.SlideIndex); Start-Sleep 2
$copy.Shapes.Item($b).Delete()
$copy.Shapes.Item($a).Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('act:param.helix')) 'a gear on the copy is changed (helix)'
Start-Sleep 3
$S.Window.Selection.Unselect()
$c = $addin.Insert('mech.spur.axial'); $sc = $copy.Shapes.Item($c); $sc.Left = 560; $sc.Top = 120
$S.Window.Selection.Unselect(); Start-Sleep 2

Assert-Isk $S ($addin.CompareWithPrevious() -eq '') 'the comparison runs'
Start-Sleep 2
$marks = @($copy.Shapes | Where-Object { try { $_.Tags.Item('ISK_DIFF') -eq '1' } catch { $false } })
Write-IskInfo ('marks: ' + (($marks | ForEach-Object { $_.Name }) -join ', '))
Assert-Isk $S ($marks.Count -eq 4) "added, changed and removed are marked, with a legend ($($marks.Count))"
Save-IskShot $S '1-compared'
Assert-Isk $S ($addin.CompareWithPrevious() -eq '') 'called again'
Start-Sleep 1
$left = @($copy.Shapes | Where-Object { try { $_.Tags.Item('ISK_DIFF') -eq '1' } catch { $false } }).Count
Assert-Isk $S ($left -eq 0) 'the marks are removed again'
