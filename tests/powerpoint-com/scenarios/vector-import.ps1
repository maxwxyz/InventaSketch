# Scenario: "Review ▸ Import …" with drawings – an SVG with units keeps its real size (80 x 50 mm) as one unit of native,
# editable shapes; a DXF in millimetres becomes a second unit. The ribbon (with the new Review group) is loaded.
param($S)
$addin = $S.AddIn

Assert-Isk $S ($addin.RibbonLoaded()) 'the ribbon with the Review group is loaded'
$svg = "<svg xmlns='http://www.w3.org/2000/svg' width='80mm' height='50mm' viewBox='0 0 80 50'>" +
       "<rect x='0' y='0' width='80' height='50' rx='3'/><circle cx='25' cy='25' r='12'/>" +
       "<path d='M45 40 L55 10 Q65 0 75 25'/><text x='5' y='46' font-size='4'>Fig. 1</text></svg>"
$before = @(Get-IskShapes $S 'ISK:*').Count
Assert-Isk $S ($addin.ImportData('drawing.svg', $svg) -eq '') 'the SVG is imported'
Start-Sleep 3
$units = @(Get-IskShapes $S 'ISK:*')
Assert-Isk $S ($units.Count -eq $before + 1) "one unit for the whole drawing ($($units.Count - $before))"
$unit = $units | Select-Object -Last 1
$wMm = $unit.Width / 72 * 25.4; $hMm = $unit.Height / 72 * 25.4
Write-IskInfo ("SVG unit {0}: {1:N1} x {2:N1} mm, {3} shapes" -f $unit.Name, $wMm, $hMm, $unit.GroupItems.Count)
Assert-Isk $S ([math]::Abs($wMm - 80) -lt 1.5 -and [math]::Abs($hMm - 50) -lt 1.5) 'real size 80 x 50 mm'
Assert-Isk $S ($unit.GroupItems.Count -ge 4) 'native shapes: frame, circle, curve, text'
# The frame's 3 mm corner radius (rx) on its 50 mm short side: adjustment 0.06, not PowerPoint's default 1/6.
$round = @($unit.GroupItems | Where-Object { try { $_.AutoShapeType -eq 5 } catch { $false } }) | Select-Object -First 1
$adj = if ($round) { $round.Adjustments.Item(1) } else { -1 }
Assert-Isk $S ([math]::Abs($adj - 0.06) -lt 0.005) "the corner radius is kept (adjustment $adj)"
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '1-svg'

$dxf = (@('0','SECTION','2','HEADER','9','$INSUNITS','70','4','0','ENDSEC','0','SECTION','2','ENTITIES',
          '0','LINE','8','0','10','0','20','0','11','40','21','0',
          '0','CIRCLE','8','0','10','20','20','10','40','8',
          '0','LWPOLYLINE','8','0','90','2','70','0','10','0','20','20','42','1','10','40','20','20',
          '0','ENDSEC','0','EOF') -join "`n")
Assert-Isk $S ($addin.ImportData('part.dxf', $dxf) -eq '') 'the DXF is imported'
Start-Sleep 3
$after = @(Get-IskShapes $S 'ISK:*')
Assert-Isk $S ($after.Count -eq $before + 2) "a second unit for the DXF ($($after.Count - $before))"
$S.Window.Selection.Unselect(); Start-Sleep 1
Save-IskShot $S '2-dxf'
