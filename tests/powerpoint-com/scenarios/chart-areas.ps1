# Scenario (charts, maintainer 2026-11-25: "insert hatched/patterned areas under curves, between two or more curves ..."):
# a figure with two curves (imported from matplotlib code); on curve 1 "Fill below > //" adds a hatched area below it, "Fill
# to > curve 2" an area between the two curves; the area is a part: its "Pattern" dropdown lists every pattern with a
# picture, the current one marked; picking "x" changes it; the hatch is drawn natively (lines in PowerPoint); Del deletes
# the selected area.
param($S)
$addin = $S.AddIn

function Get-Fig { Get-IskShapes $S 'ISK:chart*' | Select-Object -First 1 }
function Get-Regions($shape) {
    # The figure specification is a JSON string inside the model: its "regions" array. The model JSON escapes the quotes
    # of that string as " (System.Text.Json's default encoder), so \uXXXX and \" are decoded first.
    $model = [regex]::Replace((Get-IskModel $shape), '\\u([0-9a-fA-F]{4})', { param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) })
    $model = $model -replace '\\"', '"'
    $m = [regex]::Match($model, '"regions"\s*:\s*\[(.*?)\]\s*[,}]')
    if ($m.Success) { $m.Groups[1].Value } else { '' }
}

$S.Window.Selection.Unselect()
$py = "import matplotlib.pyplot as plt`nfig, ax = plt.subplots()`nax.plot([0, 1, 2, 3, 4], [1, 3, 2, 4, 3.5], label='measured')`nax.plot([0, 1, 2, 3, 4], [2, 2, 2.5, 2.5, 3], label='model')`n"
$report = $addin.ImportCode($py); Start-Sleep 3
Write-IskInfo ("import: " + $report)
$fig = Get-Fig
Assert-Isk $S ($null -ne $fig) 'the figure was imported'
if ($null -eq $fig) { return }
$fig.Left = 160; $fig.Top = 100; $fig.Width = 480; $fig.Height = 300
$fig.Select(); Start-Sleep 2
$r = Get-IskRect $S $fig.Name
$around = @(($r.X - 40), ($r.Y - 60), ($r.W + 80), ($r.H + 120))

# 1) Curve 1: "Fill below" with the dense hatch "//".
$addin.Command('partsel:series.0') | Out-Null; Start-Sleep 2
$addin.Command('pick:@p.chart.area.below:chart.area.below=//') | Out-Null; Start-Sleep 3
$fig = Get-Fig
$g1 = Get-Regions $fig
Write-IskInfo ("regions: " + $g1)
Assert-Isk $S ($g1 -match '"kind":"below"' -and $g1 -match '"hatch":"//"') 'a hatched area (//) below curve 1 was added'
Save-IskShot $S '1-fill-below' -Around $around

# 2) Curve 1: "Fill to > curve 2" (between the two curves).
$fig.Select(); Start-Sleep 2
$addin.Command('partsel:series.0') | Out-Null; Start-Sleep 2
$addin.Command('pick:@p.chart.area.to:chart.area.to=curve.1') | Out-Null; Start-Sleep 3
$fig = Get-Fig
$g2 = Get-Regions $fig
Write-IskInfo ("regions: " + $g2)
Assert-Isk $S ($g2 -match '"kind":"between","a":0,"b":1') 'an area between curve 1 and curve 2 was added'
# Natively drawn: the hatch is made of PowerPoint lines (no picture).
$pictures = @($fig.GroupItems | Where-Object { $_.Type -eq 13 }).Count
Assert-Isk $S ($fig.Type -eq 6 -and $pictures -eq 0) "the areas are native shapes and lines ($pictures pictures)"
Save-IskShot $S '2-fill-between' -Around $around

# 3) The area is a part: its "Pattern" dropdown lists the patterns with pictures, the current one marked; pick "x".
$fig.Select(); Start-Sleep 2
$addin.Command('partsel:region.1') | Out-Null; Start-Sleep 2
$bar = [string]$addin.BarIds()
Write-IskInfo ("bar of the area: " + $bar)
Assert-Isk $S (($bar -split ',') -contains 'choice:@p.chart.area.pattern') 'the area bar has the "Pattern" dropdown'
$addin.Command('choice:@p.chart.area.pattern') | Out-Null; Start-Sleep 2
$rows = @(([string]$addin.DropItems()) -split "`n" | Where-Object { $_ })
Write-IskInfo ("dropdown: " + ($rows -join ' | '))
Assert-Isk $S ($rows.Count -ge 12) "the dropdown lists the patterns ($($rows.Count))"
Assert-Isk $S (@($rows | Where-Object { $_ -match '\(picture\)' }).Count -eq $rows.Count) 'every pattern has a picture'
Assert-Isk $S (@($rows | Where-Object { $_ -match '\(current\)' }).Count -eq 1) 'the current pattern is marked'
Save-IskShot $S '3-pattern-dropdown' -Around $around
$addin.Command('pick:@p.chart.area.pattern:chart.area.pattern=x') | Out-Null; Start-Sleep 3
$g3 = Get-Regions (Get-Fig)
Assert-Isk $S ($g3 -match '"hatch":"x"') 'picking "x" changes the pattern of the area'
Save-IskShot $S '4-pattern-x' -Around $around

# 4) Del deletes the selected area, not the figure.
$fig = Get-Fig; $fig.Select(); Start-Sleep 2
$addin.Command('partsel:region.1') | Out-Null; Start-Sleep 1
[IskTest.Native]::keybd_event(0x2E, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x2E, 0, 2, [UIntPtr]::Zero)
Start-Sleep 3
$fig = Get-Fig
Assert-Isk $S ($null -ne $fig) 'Del did not delete the figure'
$g4 = Get-Regions $fig
Assert-Isk $S ($g4 -match '"kind":"below"' -and $g4 -notmatch '"kind":"between"') 'Del deleted the selected area only'
Send-IskEscape; Start-Sleep 1
