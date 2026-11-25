# Scenario: compatibility check (drawing rules) – findings for a coloured element on slide 1 and a gear on slide 2, with the slide and
# the unit to navigate to; the window opens with the list and the disclaimer.
param($S)
$addin = $S.AddIn

$bat = $addin.Insert('energy.storage.battery')
$S.Window.Selection.Unselect()
$slide2 = $S.App.ActivePresentation.Slides.Add(2, 12)
$S.Window.View.GotoSlide(2); Start-Sleep 2
$gear = $addin.Insert('mech.spur.axial')
Start-Sleep 2
$lines = @(($addin.PatentCheck('epo')) -split "`n" | Where-Object { $_ })
Write-IskInfo ("{0} finding(s); first: {1}" -f $lines.Count, ($lines | Select-Object -First 3) -join ' / ')
Assert-Isk $S (-not ($lines -match '^error')) 'the compatibility check runs'
Assert-Isk $S ($lines.Count -gt 0) 'it reports findings for the EPO profile'
Assert-Isk $S (@($lines | Where-Object { $_ -like "1|*|*|$bat" }).Count -gt 0) 'a finding points at the coloured element on slide 1'
Assert-Isk $S (@($lines | Where-Object { $_ -like '2|*' }).Count -gt 0) 'slide 2 is checked too'
Assert-Isk $S ($addin.ShowPatentCheck($true)) 'the compatibility check window opens'
Start-Sleep 4
Save-IskShot $S '1-compatibility-check'
$addin.ShowPatentCheck($false) | Out-Null
