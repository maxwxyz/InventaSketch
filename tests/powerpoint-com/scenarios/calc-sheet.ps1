# Scenario (Calc 6), ASCII only: the calculation sheet of a resistor with entered values lists the values with formula and source;
# "Insert as table" puts it on the slide as an annotation table (one unit, one undo step).
param($S)
$addin = $S.AddIn
$S.Window.Selection.Unselect()
Assert-Isk $S ($addin.SetValueLevel('explore')) 'the value level can be set to Explore'
$name = $addin.Insert('elec.resistor'); Start-Sleep 1
$r1 = $S.Slide.Shapes.Item($name)
$m = [string]$addin.SetValue($name, 'U', '230 V'); Start-Sleep 1
$r1 = @(Get-IskShapes $S 'ISK:elec.resistor*')[0]
# Non-ASCII characters are built from their codes (Windows PowerShell 5.1 reads scripts without BOM as ANSI).
$ohm = [char]0x03A9; $pm = [char]0x00B1; $ell = [char]0x2026
$m = [string]$addin.SetValue($r1.Name, 'R', "100 $ohm $($pm)10 %"); Start-Sleep 1
$r1 = @(Get-IskShapes $S 'ISK:elec.resistor*')[0]
$r1.Select(); Start-Sleep 2
$csv = [string]$addin.CalcSheet('csv', $false)
Write-IskInfo ("csv: " + ($csv -replace "`r?`n", ' / ').Substring(0, [Math]::Min(300, $csv.Length)))
Assert-Isk $S ($csv -match '^Element;Gr.+;Zeichen;Wert;Einheit;Bereich;Formel;Herkunft;Quelle') 'the sheet has the German header'
Assert-Isk $S ($csv -match ';I;' -and $csv -match '\[S7\]') 'the current is listed with its source'
Assert-Isk $S ($csv -match "2,09 $ell 2,56") 'the current has its tolerance range'
$md = [string]$addin.CalcSheet('markdown', $false)
Assert-Isk $S ($md -match 'Quellen:') 'the Markdown sheet names the sources'
$before = @(Get-IskShapes $S 'ISK:ann.table*').Count
[void]$addin.CalcSheet('table', $true); Start-Sleep 3
$tables = @(Get-IskShapes $S 'ISK:ann.table*')
Assert-Isk $S ($tables.Count -eq $before + 1) 'Insert as table puts one table on the slide'
if ($tables.Count -gt 0) { Assert-Isk $S ((Get-IskModel $tables[-1]) -match 'Formel|Formula') 'the table holds the sheet' }
Save-IskShot $S '1-sheet-table'
[void]$addin.SetValueLevel('quick')
