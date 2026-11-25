# Scenario (roadmap 4b): plotting and drawing code is read (never executed) and imported as editable elements:
# a matplotlib script with two stacked subplots, pgfplots and a TikZ drawing.
param($S)
$addin = $S.AddIn

$py = @"
import numpy as np
import matplotlib.pyplot as plt
t = np.linspace(0, 10, 101)
fig, (a1, a2) = plt.subplots(2, 1, sharex=True)
a1.plot(t, np.sin(t), label='sin')
a1.axhline(0.5, linestyle='--')
a2.plot(t, np.exp(-t / 3), 'r-', label='decay')
a2.set_xlabel('t in s')
a1.set_title('Example')
"@
$S.Window.Selection.Unselect()
$n0 = @(Get-IskShapes $S).Count
$report = $addin.ImportCode($py); Start-Sleep 2
Write-IskInfo ("matplotlib: " + ($report -replace "`n", ' | '))
Assert-Isk $S (@(Get-IskShapes $S).Count -gt $n0) 'the matplotlib script becomes an element'
$fig = @(Get-IskShapes $S) | Select-Object -Last 1
Assert-Isk $S ((Get-IskModel $fig) -match 'chart\.figure') 'stacked subplots become a figure'
$fig.Left = 20; $fig.Top = 20

$tex = @"
\begin{tikzpicture}
\draw[thick,->] (0,0) -- (3,0) node[right] {x};
\draw[dashed,red] (0,0) rectangle (2,1);
\draw (1.5,1.5) circle (0.5);
\node at (1,-0.5) {Label};
\end{tikzpicture}
"@
$n1 = @(Get-IskShapes $S).Count
$report = $addin.ImportCode($tex); Start-Sleep 2
Write-IskInfo ("tikz: " + ($report -replace "`n", ' | '))
Assert-Isk $S (@(Get-IskShapes $S).Count -gt $n1) 'the TikZ drawing becomes an element'
Save-IskShot $S '1-imported'
Send-IskEscape; Start-Sleep 1
