<#
.SYNOPSIS
  Runs the PowerPoint UI scenarios of the InventaSketch COM add-in (tests/powerpoint-com/scenarios/*.ps1).

.DESCRIPTION
  Each scenario runs in its own PowerShell process with its own PowerPoint (started through Explorer) and a blank
  slide: it drives the add-in through its automation object and the mouse, checks numbers read back from the shapes'
  sketch models, takes screenshots and quits PowerPoint without saving. A separate process per scenario releases every
  COM reference when it ends, so PowerPoint really exits. PowerPoint must be closed before the run; the run refuses to
  touch open presentations. Screenshots and logs go to local-resources/powerpoint-tests/<timestamp>/<scenario>/
  (git-ignored). Do not use mouse or keyboard while the scenarios run.
  In a scenario, never name a variable $s: PowerShell names are case-insensitive and $s is the session $S.

.EXAMPLE
  .\tests\powerpoint-com\Invoke-IskScenarios.ps1 -List
  .\tests\powerpoint-com\Invoke-IskScenarios.ps1 -Name explode-linkages, linkage-build
  .\tests\powerpoint-com\Invoke-IskScenarios.ps1            # all scenarios
#>
param(
    [string[]]$Name = @(),
    [switch]$List,
    [string]$OutDir,
    # Internal: run exactly this scenario in the current process and write result.json.
    [string]$Single
)

$ErrorActionPreference = 'Stop'
$all = Get-ChildItem (Join-Path $PSScriptRoot 'scenarios') -Filter '*.ps1' | Sort-Object Name

if ($Single) {
    Import-Module (Join-Path $PSScriptRoot 'IskPowerPoint.psm1') -Force
    $file = $all | Where-Object BaseName -eq $Single
    $dir = Join-Path $OutDir $Single
    $S = $null; $err = $null; $foreign = $false
    try {
        $S = Start-IskSession -Scenario $Single -OutDir $dir
        & $file.FullName -S $S
    }
    catch {
        $err = $_.Exception.Message
        $foreign = [bool]$_.Exception.Data['IskForeign']
        Write-Host "  ERROR $err" -ForegroundColor Red
        Write-Host ($_.InvocationInfo.PositionMessage) -ForegroundColor DarkGray
    }
    finally {
        # A PowerPoint that was open before the run is never closed.
        if (-not $foreign) { Stop-IskSession $S -StartedHere | Out-Null }
    }
    $failures = @(if ($S) { $S.Failures })
    if ($err) { $failures += "error: $err" }
    New-Item -ItemType Directory -Force $dir | Out-Null
    [pscustomobject]@{ Passed = $(if ($S) { $S.Passes } else { 0 }); Failures = $failures; Foreign = $foreign } |
        ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $dir 'result.json')
    exit $failures.Count
}

if ($List) {
    foreach ($f in $all) {
        $synopsis = (Get-Content $f.FullName | Where-Object { $_ -match '^\s*#\s*Scenario:' } | Select-Object -First 1) -replace '^\s*#\s*Scenario:\s*', ''
        '{0,-20} {1}' -f $f.BaseName, $synopsis
    }
    return
}
$unknown = $Name | Where-Object { $all.BaseName -notcontains $_ }
if ($unknown) { throw "Unknown scenario(s): $($unknown -join ', '). Use -List." }
$selected = if ($Name.Count) { $all | Where-Object { $Name -contains $_.BaseName } } else { $all }
if (-not $OutDir) {
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $OutDir = Join-Path $root ('local-resources\powerpoint-tests\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

$results = @()
foreach ($f in $selected) {
    Write-Host "== $($f.BaseName)" -ForegroundColor Cyan
    powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Single $f.BaseName -OutDir $OutDir
    for ($i = 0; $i -lt 120 -and (Get-Process POWERPNT -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
    $running = [bool](Get-Process POWERPNT -ErrorAction SilentlyContinue)
    $resultFile = Join-Path (Join-Path $OutDir $f.BaseName) 'result.json'
    $r = if (Test-Path $resultFile) { Get-Content $resultFile -Raw | ConvertFrom-Json } else { [pscustomobject]@{ Passed = 0; Failures = @('no result'); Foreign = $false } }
    $failures = @($r.Failures | Where-Object { $_ })
    $results += [pscustomobject]@{
        Scenario = $f.BaseName
        Result   = if ($failures.Count) { 'FAIL' } else { 'ok' }
        Passed   = $r.Passed
        Failed   = $failures.Count
        Details  = $failures -join '; '
    }
    if ($r.Foreign) { break }
    if ($running) { Write-Host '  WARNING PowerPoint is still running; stopping the run.' -ForegroundColor Yellow; break }
}

Write-Host ''
$results | Format-Table Scenario, Result, Passed, Failed, Details -AutoSize -Wrap | Out-String -Width 200 | Write-Host
Write-Host "Screenshots: $OutDir"
exit (@($results | Where-Object Result -ne 'ok').Count)
