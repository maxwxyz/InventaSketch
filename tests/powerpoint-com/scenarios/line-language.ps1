# Scenario: line language on a wire (extend by its end "+", draw on, split at a corner) and a leader docked on a surface.
param($S)
$addin = $S.AddIn
$k = Get-IskScale

$w = $addin.Insert('elec.wire.free')
# The add-in may redraw the unit after a move (new shape): set the position, then look the wire up again.
function Wire { @(Get-IskShapes $S 'ISK:elec.wire*')[0] }
$ws = $S.Slide.Shapes.Item($w); $ws.Left = 150; Start-Sleep 1
$ws = Wire; $ws.Top = 330; Start-Sleep 1
$ws = Wire
$ws.Select(); Start-Sleep 3
$n0 = (Get-IskPoints $ws).Count
$r = Get-IskRect $S $w
Save-IskShot $S '0-selected' -Around ($r.X - 200), ($r.Y - 200), ($r.W + 400), 400
# Extend: drag the "+" beyond the right end.
Invoke-IskDrag -From @(($r.X + $r.W + 20 * $k), $r.Y) -To @(($r.X + $r.W + 20 * $k + 120), ($r.Y + 90))
# The wire after the edit (redrawn under a new shape, so found by type).
function Wire { @(Get-IskShapes $S 'ISK:elec.wire*')[0] }
$ws = Wire
$n1 = (Get-IskPoints $ws).Count
Assert-Isk $S ($n1 -gt $n0) "dragging the end '+' extends the wire ($n0 -> $n1 points)"

# Draw on: drag from the end "+", click one more point, Esc.
$ws.Select(); Start-Sleep 3
$r = Get-IskRect $S $ws.Name
$ex = $r.X + $r.W; $ey = $r.Y + $r.H
# The end "+" where the handle layout placed it (automation Handles(): "id@x,y,r;…"); the old fixed offset as fallback.
$endPlus = @(([string]$addin.Handles()) -split ';' | ForEach-Object {
        $m = [regex]::Match($_, '^(end:1|port:\d+)@(-?\d+),(-?\d+)')
        if ($m.Success) { [pscustomobject]@{ Id = $m.Groups[1].Value; X = [double]$m.Groups[2].Value; Y = [double]$m.Groups[3].Value } }
    }) | Sort-Object { [Math]::Abs($_.X - $ex) + [Math]::Abs($_.Y - $ey) } | Select-Object -First 1
# Dragging the end "+" draws on (a click on it opens the insert box); release sets the point, a click adds a corner.
if (-not $endPlus) { $endPlus = [pscustomobject]@{ Id = 'fallback'; X = $ex + 14 * $k; Y = $ey + 11 * $k } }
Write-IskInfo ("end + at {0},{1} ({2})" -f $endPlus.X, $endPlus.Y, $endPlus.Id)
Invoke-IskDrag -From @($endPlus.X, $endPlus.Y) -To @(($ex + 160), ($ey + 10))
Start-Sleep -Milliseconds 600
Invoke-IskClick ($ex + 160) ($ey - 120) -WaitMs 600
Save-IskShot $S '1-drawing'
Send-IskEscape; Start-Sleep 4
$ws = Wire
$n2 = (Get-IskPoints $ws).Count
Assert-Isk $S ($n2 -gt $n1) "drawing on from the end '+' adds points ($n1 -> $n2)"
Save-IskShot $S '2-drawn'

Wire | ForEach-Object { $_.Select() }; Start-Sleep 3
$addin.Command('pt:split:1') | Out-Null; Start-Sleep 4
Assert-Isk $S (@(Get-IskShapes $S 'ISK:elec.wire*').Count -eq 2) 'splitting at corner 1 gives two wires'

# A leader whose tip is dragged into a rectangle docks on its surface and follows it.
$S.Window.Selection.Unselect()
$rect = $addin.Insert('basic.rect')
$rs = $S.Slide.Shapes.Item($rect); $rs.Left = 650; $rs.Top = 300
$leader = $addin.Insert('ann.leader')
$ls = $S.Slide.Shapes.Item($leader); $ls.Left = 520; $ls.Top = 200
$ls.Select(); Start-Sleep 3
$rr = Get-IskRect $S $rect; $lr = Get-IskRect $S $leader
# Tip = lower-left corner of the leader's bounds; target 30 % / 35 % into the rectangle (no snap point there).
Invoke-IskDrag -From @($lr.X, ($lr.Y + $lr.H)) -To @(($rr.X + $rr.W * 0.3), ($rr.Y + $rr.H * 0.35))
$ls = @(Get-IskShapes $S 'ISK:ann.leader*')[0]
Assert-Isk $S ((Get-IskModel $ls) -match '"attach"') 'the leader tip docks on the rectangle'
$tip = (Get-IskPoints $ls)[0]
Save-IskShot $S '3-leader-docked'
$rs = @(Get-IskShapes $S 'ISK:basic.rect*')[0]
$rs.Select(); Start-Sleep 2
$rs.Left = $rs.Left + 40; Start-Sleep 5
$ls = @(Get-IskShapes $S 'ISK:ann.leader*')[0]
$moved = (Get-IskPoints $ls)[0]
Assert-Isk $S ([Math]::Abs($moved[0] - $tip[0] - 40 * 25.4 / 72) -lt 0.5) ('the docked tip follows the rectangle ({0} -> {1})' -f (Format-IskPoints @(, $tip)), (Format-IskPoints @(, $moved)))
Save-IskShot $S '4-leader-followed'
