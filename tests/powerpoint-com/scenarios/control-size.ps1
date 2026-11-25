# Scenario (interaction concept section 9 items 9 and 10): "Larger controls (125 %)" scales the bar and the handles on the
# slide together; the layout keeps the handles apart. Back to normal size at the end.
param($S)
$addin = $S.AddIn

function Get-Handles {
    $raw = [string]$addin.Handles()
    , @(($raw -split ';') | Where-Object { $_ } | ForEach-Object {
        $m = [regex]::Match($_, '^(.+)@(-?\d+),(-?\d+),([\d.]+)')
        if ($m.Success) { [pscustomobject]@{ Id = $m.Groups[1].Value; X = [double]$m.Groups[2].Value; Y = [double]$m.Groups[3].Value; R = [double]::Parse($m.Groups[4].Value, [Globalization.CultureInfo]::InvariantCulture) } }
    })
}
function BarWidth { $b = ([string]$addin.BarRect()) -split ','; if ($b.Count -ge 4) { [double]$b[2] } else { 0 } }

$addin.SetControlSize(1.0) | Out-Null
$S.Window.Selection.Unselect()
$name = $addin.Insert('mech.spur.side'); Start-Sleep 1
$g = $S.Slide.Shapes.Item($name); $g.Select(); Start-Sleep 3
$h0 = Get-Handles; $w0 = BarWidth
$port0 = $h0 | Where-Object { $_.Id -like 'port:*' } | Select-Object -First 1
Write-IskInfo ("normal: bar {0} px, {1} r={2}" -f $w0, $port0.Id, $port0.R)
Save-IskShot $S '1-normal'

Assert-Isk $S ($addin.SetControlSize(1.25)) 'the control size can be set to 125 %'
Start-Sleep 3
$h1 = Get-Handles; $w1 = BarWidth
$port1 = $h1 | Where-Object { $_.Id -eq $port0.Id } | Select-Object -First 1
Write-IskInfo ("125 %: bar {0} px, {1} r={2}" -f $w1, $port1.Id, $port1.R)
Assert-Isk $S ($w0 -gt 0 -and [Math]::Abs($w1 / $w0 - 1.25) -lt 0.08) ("the bar is a quarter wider ({0} -> {1})" -f $w0, $w1)
# The layout keeps the larger handles apart: the add-in logs "(overlap!)" after the handle list otherwise.
$log = Get-IskLog $S
$last = Get-Content $log | Where-Object { $_ -match 'Handles: ' } | Select-Object -Last 1
Write-IskInfo ("layout: " + $last.Substring([Math]::Max(0, $last.Length - 160)))
Assert-Isk $S ($last -and $last -notmatch 'overlap!') 'the larger handles do not overlap'
$moved = $port1 -and $port0 -and ([Math]::Abs($port1.X - $port0.X) + [Math]::Abs($port1.Y - $port0.Y)) -gt 0
Write-IskInfo ("{0} moved out with the larger size: {1}" -f $port0.Id, $moved)
Save-IskShot $S '2-larger'
$addin.SetControlSize(1.0) | Out-Null
