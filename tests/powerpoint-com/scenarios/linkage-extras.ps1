# Scenario: an exploded four-bar gets a coupler curve and ghost positions from the bar of one of its links.
param($S)
$addin = $S.AddIn

# Every command of the selected element (bar and pie): automation CommandIds(), else the add-in log line "Commands: ...".
function Get-CommandIds {
    $raw = $null
    try { $raw = [string]$S.AddIn.CommandIds() } catch { $raw = $null }
    if (-not $raw) {
        $line = Get-Content (Get-IskLog $S) | Where-Object { $_ -match 'Commands: ' } | Select-Object -Last 1
        if ($line) { $raw = $line -replace '.*Commands: ', '' }
    }
    , @(($raw -split ',') | Where-Object { $_ })
}
function Wait-CommandIds([scriptblock]$Until, [int]$Seconds = 10) {
    $ids = @(); $end = (Get-Date).AddSeconds($Seconds)
    do { $ids = Get-CommandIds; if (& $Until $ids) { break }; Start-Sleep -Milliseconds 700 } while ((Get-Date) -lt $end)
    , $ids
}

$n = $addin.Insert('kin.fourbar')
$m = $S.Slide.Shapes.Item($n); $m.Left = 200; $m.Top = 120; $m.Select(); Start-Sleep 3
Assert-Isk $S ($addin.Command('explode')) 'the four-bar explodes'
Start-Sleep 6
$links = @(Get-IskShapes $S 'ISK:kin.link*')
Assert-Isk $S ($links.Count -eq 3) "three links ($($links.Count))"
$links[1].Select(); Start-Sleep 3
$ids = Get-CommandIds
Write-IskInfo ('link bar: ' + ($ids -join ' '))
Assert-Isk $S (($ids -contains 'trace') -and ($ids -contains 'ghosts')) 'a link of a chain with F = 1 offers path and ghost positions'
$addin.Command('trace') | Out-Null; Start-Sleep 5
Assert-Isk $S (@(Get-IskShapes $S 'ISK:kin.motion.path*').Count -eq 1) 'the coupler curve is inserted as a motion path'
@(Get-IskShapes $S 'ISK:kin.link*')[1].Select(); Start-Sleep 3
$before = $S.Slide.Shapes.Count
$addin.Command('ghosts') | Out-Null; Start-Sleep 5
Assert-Isk $S ($S.Slide.Shapes.Count -eq $before + 1) 'the ghost positions are inserted as one element'
$S.Window.Selection.Unselect(); Start-Sleep 2
Save-IskShot $S '1-curve-and-ghosts'
