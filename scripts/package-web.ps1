<#
.SYNOPSIS
  Builds the installable web add-in package dist\InventaSketch-Web (Windows, macOS, Linux) and a ZIP of it.
.DESCRIPTION
  Layout (expected by the local host and the install scripts):
    App\          task pane (taskpane.html, assets, .NET WebAssembly engine in _framework)
    Server\       InventaSketch.WebHost.exe (Windows, .NET Framework 4.8)
    manifest.xml  add-in manifest for https://localhost:3217/
    *.cmd         Windows install/uninstall/start/stop (double-click)
    *.sh, *.command, inventasketch_web.py   macOS/Linux (Python 3)
  The web host and the install scripts were ported from Slidemax with the copyright holder's approval.
.PARAMETER Port
  Local HTTPS port written into the manifest (default 3217; Slidemax-Web uses 3000).
#>
param([int]$Port = 3217, [switch]$SkipWebBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root 'hosts\office-web'
$out = Join-Path $root 'dist\InventaSketch-Web'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = $null
foreach ($c in @((Get-Command dotnet -ErrorAction SilentlyContinue).Source, (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'))) {
    if ($c -and (Test-Path $c) -and ((& $c --list-sdks 2>$null) -match '^10\.')) { $dotnet = $c; break }
}
if (-not $dotnet) { throw 'No .NET 10 SDK found; run scripts\build.ps1 once (installs it user-locally).' }
$env:PATH = (Split-Path $dotnet -Parent) + ';' + $env:PATH
if (-not (Get-Command npm -ErrorAction SilentlyContinue) -and (Test-Path 'C:\Program Files\nodejs\npm.cmd')) { $env:PATH = 'C:\Program Files\nodejs;' + $env:PATH }

if (-not $SkipWebBuild) {
    Push-Location $web
    try {
        if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' } }
        node scripts/engine.mjs --if-missing; if ($LASTEXITCODE -ne 0) { throw 'engine publish failed' }
        node scripts/icons.mjs; if ($LASTEXITCODE -ne 0) { throw 'icons failed' }
        npx tsc -p tsconfig.json --noEmit; if ($LASTEXITCODE -ne 0) { throw 'typecheck failed' }
        npx vite build; if ($LASTEXITCODE -ne 0) { throw 'vite build failed' }
        node scripts/manifest.mjs "https://localhost:$Port/"; if ($LASTEXITCODE -ne 0) { throw 'manifest failed' }
    } finally { Pop-Location }
}

& $dotnet build (Join-Path $root 'tools\InventaSketch.WebHost\InventaSketch.WebHost.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'web host build failed' }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $out 'App'), (Join-Path $out 'Server') | Out-Null
Copy-Item (Join-Path $web 'dist\*') (Join-Path $out 'App') -Recurse -Force
Remove-Item (Join-Path $out 'App\manifest.xml'), (Join-Path $out 'App\dev.html') -ErrorAction SilentlyContinue
Copy-Item (Join-Path $web 'dist\manifest.xml') (Join-Path $out 'manifest.xml')
Copy-Item (Join-Path $root 'tools\InventaSketch.WebHost\bin\Release\net48\*') (Join-Path $out 'Server') -Recurse -Force
Get-ChildItem (Join-Path $out 'Server') -Filter *.pdb | Remove-Item
Copy-Item (Join-Path $web 'local\*') $out -Recurse -Force
Copy-Item (Join-Path $root 'LICENSE') $out

$zip = Join-Path $root 'dist\InventaSketch-Web.zip'
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "Package: $out"
Write-Host "ZIP:     $zip"
