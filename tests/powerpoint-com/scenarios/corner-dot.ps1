# Scenario (interaction concept section 9 item 6): a click on the corner dot of a segment adds the corner exactly where the
# dot is drawn (the handle layout may slide the dot along its segment, away from the middle).
param($S)
$addin = $S.AddIn
$k = Get-IskScale

function Get-Handles {
    $raw = [string]$addin.Handles()
    , @(($raw -split ';') | Where-Object { $_ } | ForEach-Object {
        $m = [regex]::Match($_, '^(.+)@(-?\d+),(-?\d+),([\d.]+)$')
        if ($m.Success) { [pscustomobject]@{ Id = $m.Groups[1].Value; X = [double]$m.Groups[2].Value; Y = [double]$m.Groups[3].Value } }
    })
}
function Wire { @(Get-IskShapes $S 'ISK:elec.wire*')[0] }

$addin.Insert('elec.wire.free') | Out-Null; Start-Sleep 1
$ws = Wire; $ws.Left = 200; $ws.Top = 300
Start-Sleep 1
$ws = Wire; $ws.Select(); Start-Sleep 3
$p0 = Get-IskPoints $ws
$h = Get-Handles
$dot = $h | Where-Object { $_.Id -eq 'seg:0' } | Select-Object -First 1
Write-IskInfo ('handles: ' + (($h | ForEach-Object { '{0}@{1},{2}' -f $_.Id, $_.X, $_.Y }) -join ' '))
Assert-Isk $S ($null -ne $dot) 'the first segment has a corner dot'
if ($dot) {
    # Hover first (the dot fades in near the pointer), then click without moving.
    Move-IskMouse ($dot.X + 30) ($dot.Y + 20); Start-Sleep -Milliseconds 400
    Move-IskMouse $dot.X $dot.Y; Start-Sleep -Milliseconds 600
    [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 120
    [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep 3
    $ws = Wire
    $p1 = Get-IskPoints $ws
    Write-IskInfo ("points: {0} -> {1}" -f (Format-IskPoints $p0), (Format-IskPoints $p1))
    Assert-Isk $S ($p1.Count -eq $p0.Count + 1) 'a click on the dot adds one corner'
    if ($p1.Count -eq $p0.Count + 1) {
        $px = ConvertTo-IskPixel $S $p1[1]
        $dist = Get-IskDistance $px @($dot.X, $dot.Y)
        Write-IskInfo ("new corner at pixel {0:N0},{1:N0}; dot at {2:N0},{3:N0}; distance {4:N1} px" -f $px[0], $px[1], $dot.X, $dot.Y, $dist)
        Assert-Isk $S ($dist -lt 4 * $k) 'the new corner is under the dot'
    }
}
Save-IskShot $S '1-corner-added'
