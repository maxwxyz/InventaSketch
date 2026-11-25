# Regression (maintainer 2026-11-25): "elements stay on the cursor after a click, e.g. charts". A plain click (press and
# release, no movement) on an unselected element, then moving the mouse, must not move the element.
param($S)
$addin = $S.AddIn
function Click-Move($label, $x, $y, $name) {
    $sh = $S.Slide.Shapes.Item($name); $l0 = $sh.Left; $t0 = $sh.Top
    Move-IskMouse $x $y; Start-Sleep -Milliseconds 400
    [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120
    [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 1200
    for ($i = 1; $i -le 10; $i++) { Move-IskMouse ($x + 12 * $i) ($y + 8 * $i); Start-Sleep -Milliseconds 60 }
    Start-Sleep 1
    $sh = @(Get-IskShapes $S) | Where-Object { $_.Name -eq $name } | Select-Object -First 1
    $moved = [math]::Abs($sh.Left - $l0) + [math]::Abs($sh.Top - $t0)
    Write-IskInfo ("{0}: moved {1:N1} pt" -f $label, $moved)
    Assert-Isk $S ($moved -lt 0.5) "$label - the element does not follow the pointer"
    Send-IskEscape; $S.Window.Selection.Unselect(); Start-Sleep 1
}
$c = $addin.Insert('chart.line'); $cs = $S.Slide.Shapes.Item($c); $cs.Left = 200; $cs.Top = 120; $cs.Width = 420; $cs.Height = 280
$S.Window.Selection.Unselect(); Start-Sleep 1
$r = Get-IskRect $S $c
Click-Move 'click inside the plot' ($r.X + $r.W * 0.5) ($r.Y + $r.H * 0.75) $c
$curve = $null; foreach ($l in $S.Slide.Shapes.Item($c).GroupItems) { if ($l.Tags.Item('ISK_PART') -eq 'series.0') { $curve = $l } }
$node = $curve.Nodes.Item(3).Points; $flat = @($node | ForEach-Object { $_ })
$scr = ConvertTo-IskPixel $S @(([double]$flat[0] * 25.4 / 72), ([double]$flat[1] * 25.4 / 72))
Click-Move 'click on a curve point' $scr[0] $scr[1] $c
# Clicks on an axis and on a grid line (maintainer: "click on the chart, an axis or a grid line").
foreach ($key in @('axis.x', 'grid.major')) {
    $leaf = $null; foreach ($l in $S.Slide.Shapes.Item($c).GroupItems) { if ($l.Tags.Item('ISK_PART') -eq $key) { $leaf = $l; break } }
    if (-not $leaf) { Write-IskInfo "no $key leaf"; continue }
    # A straight line leaf has no nodes: the middle of its frame lies on it; a freeform: the middle of its first segment.
    $mx = $leaf.Left + $leaf.Width / 2; $my = $leaf.Top + $leaf.Height / 2
    try {
        if ($leaf.Nodes.Count -ge 2) {
            $pts = @($leaf.Nodes.Item(1).Points | ForEach-Object { $_ }); $pts2 = @($leaf.Nodes.Item(2).Points | ForEach-Object { $_ })
            $mx = ([double]$pts[0] + [double]$pts2[0]) / 2; $my = ([double]$pts[1] + [double]$pts2[1]) / 2
        }
    } catch { }
    $p = ConvertTo-IskPixel $S @(($mx * 25.4 / 72), ($my * 25.4 / 72))
    Click-Move "click on $key" $p[0] $p[1] $c
}
# Second click on the already selected chart's curve.
$S.Slide.Shapes.Item($c).Select(); Start-Sleep 2
Click-Move 'click on the curve of the selected chart' $scr[0] $scr[1] $c
$g = $addin.Insert('mech.spur.axial'); $gs = $S.Slide.Shapes.Item($g); $gs.Left = 700; $gs.Top = 200
$S.Window.Selection.Unselect(); Start-Sleep 1
$r = Get-IskRect $S $g
Click-Move 'click on a gear' ($r.CX) ($r.CY) $g
