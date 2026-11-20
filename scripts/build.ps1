<#
.SYNOPSIS
  Builds InventaSketch on Windows and runs all tests.
.DESCRIPTION
  Uses a .NET 10 SDK from PATH or %LOCALAPPDATA%\Microsoft\dotnet. If none is found, installs one user-locally with
  Microsoft's official dotnet-install.ps1 (no administrator rights needed). Builds the solution for all target
  frameworks, runs the xUnit tests and, if Node.js is available, builds the web add-in (hosts/office-web).
.PARAMETER Configuration
  Release (default) or Debug.
.PARAMETER SkipTests
  Build only.
.PARAMETER SkipWeb
  Do not build the web add-in.
#>
param(
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$SkipWeb
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Step([string]$text) { Write-Host ''; Write-Host "== $text" -ForegroundColor Cyan }

function Find-Dotnet {
    $candidates = @()
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    $candidates += (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) {
            $sdks = & $c --list-sdks 2>$null
            if ($sdks | Where-Object { $_ -match '^10\.' }) { return $c }
        }
    }
    return $null
}

Step 'Locate .NET 10 SDK'
$dotnet = Find-Dotnet
if (-not $dotnet) {
    $installDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
    $script = Join-Path $env:TEMP 'dotnet-install.ps1'
    Write-Host "Installing .NET 10 SDK to $installDir"
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
    & $script -Channel 10.0 -InstallDir $installDir
    $dotnet = Join-Path $installDir 'dotnet.exe'
}
Write-Host "Using $dotnet"
$env:PATH = (Split-Path $dotnet -Parent) + [IO.Path]::PathSeparator + $env:PATH

Step 'Build solution'
& $dotnet build InventaSketch.sln -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    Step 'Run tests'
    & $dotnet test InventaSketch.sln -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

Step 'Build PowerPoint COM add-in (Windows, net48)'
& $dotnet build (Join-Path $root 'hosts\powerpoint-com\InventaSketch.AddIn\InventaSketch.AddIn.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'COM add-in build failed.' }
if (-not $SkipTests) {
    & $dotnet test (Join-Path $root 'tests\InventaSketch.AddIn.Tests\InventaSketch.AddIn.Tests.csproj') -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'COM add-in tests failed.' }
}

$web = Join-Path $root 'hosts\office-web\package.json'
if (-not $SkipWeb -and (Test-Path $web)) {
    $npm = Get-Command npm -ErrorAction SilentlyContinue
    if (-not $npm -and (Test-Path 'C:\Program Files\nodejs\npm.cmd')) { $env:PATH = 'C:\Program Files\nodejs;' + $env:PATH; $npm = Get-Command npm -ErrorAction SilentlyContinue }
    if ($npm) {
        Step 'Build web add-in'
        Push-Location (Join-Path $root 'hosts\office-web')
        try {
            npm ci
            if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
            if (-not $SkipTests) { npm test; if ($LASTEXITCODE -ne 0) { throw 'Web tests failed.' } }
            npm run build
            if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
        } finally { Pop-Location }
        Step 'Package web add-in (dist\InventaSketch-Web)'
        & (Join-Path $PSScriptRoot 'package-web.ps1') -SkipWebBuild
    } else {
        Write-Warning 'Node.js not found: web add-in skipped.'
    }
}

Step 'Done'
