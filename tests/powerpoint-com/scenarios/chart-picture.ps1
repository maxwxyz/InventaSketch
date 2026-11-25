# Scenario (charts v2 stage 5): a picture of a column chart becomes an editable chart on the slide ("Chart from
# picture …" in the Review group). The picture is drawn here (axis line, five blue columns) and saved as PNG.
param($S)
$addin = $S.AddIn
Add-Type -AssemblyName System.Drawing

$png = Join-Path $S.OutDir 'columns.png'
$bmp = New-Object System.Drawing.Bitmap 400, 260
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 90, 200))
$heights = 50, 100, 75, 25, 150
for ($i = 0; $i -lt $heights.Count; $i++) { $g.FillRectangle($blue, 30 + $i * 60, 200 - $heights[$i], 36, $heights[$i]) }
$g.FillRectangle([System.Drawing.Brushes]::Black, 20, 200, 360, 1)
$g.Dispose(); $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()

$before = @(Get-IskShapes $S).Count
$note = $addin.ChartFromPicture($png)
Start-Sleep 2
Write-IskInfo ("note: " + $note)
$units = @(Get-IskShapes $S)
Assert-Isk $S ($units.Count -eq $before + 1) 'a chart is inserted'
$model = ($units | ForEach-Object { Get-IskModel $_ }) -join ' '
Assert-Isk $S ($model -match 'chart\.bar') 'it is a column chart'
Assert-Isk $S ($note -match '100') 'the note says the largest bar is 100 (no readable axis numbers)'
Save-IskShot $S '1-chart-from-picture'
