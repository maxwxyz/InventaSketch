# Scenario: "Import data …" – a GPX track becomes a map route with start and finish markers, a CSV table a chart; a second CSV
# with the chart selected replaces its data instead of adding a chart.
param($S)
$addin = $S.AddIn

$gpx = '<?xml version="1.0"?><gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1"><trk><name>Run</name><trkseg>' +
       '<trkpt lat="48.10" lon="11.50"/><trkpt lat="48.11" lon="11.52"/><trkpt lat="48.12" lon="11.51"/><trkpt lat="48.13" lon="11.54"/></trkseg></trk></gpx>'
Assert-Isk $S ($addin.ImportData('run.gpx', $gpx) -eq '') 'the GPX track is imported'
Start-Sleep 3
$routes = @(Get-IskShapes $S 'ISK:map.route*')
$markers = @(Get-IskShapes $S 'ISK:map.marker*')
Write-IskShapes $S
# Each element is its own unit (movable on its own, as after inserting): one route, start and finish markers.
Assert-Isk $S ($routes.Count -eq 1 -and $markers.Count -eq 2) "a route with start and finish markers ($($routes.Count) / $($markers.Count))"
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '1-gpx'

Assert-Isk $S ($addin.ImportData('values.csv', "x;Druck`r`n0;1,5`r`n1;2,5`r`n2;2,0") -eq '') 'the CSV table is imported'
Start-Sleep 3
$charts = @(Get-IskShapes $S 'ISK:chart.*')
Assert-Isk $S ($charts.Count -eq 1) "a chart on the slide ($($charts.Count))"
if ($charts.Count -eq 1) {
    Assert-Isk $S ((Get-IskModel $charts[0]) -match '0;1\.5') 'the decimal commas became chart data'
    $charts[0].Select(); Start-Sleep 2
    Assert-Isk $S ($addin.ImportData('more.csv', "x;Druck`n0;4`n1;5") -eq '') 'a second table goes into the selected chart'
    Start-Sleep 3
    $charts = @(Get-IskShapes $S 'ISK:chart.*')
    Assert-Isk $S ($charts.Count -eq 1 -and (Get-IskModel $charts[0]) -match '0;4') 'still one chart, with the new data'
}
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '2-chart'
