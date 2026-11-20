<#
.SYNOPSIS
  Builds the webspace package dist\InventaSketch-Webspace and dist\InventaSketch-Webspace-<version>.zip.
.DESCRIPTION
  The folder is uploaded as it is to any HTTPS webspace (Apache or IIS). It contains the built web add-in
  (hosts\office-web\dist), the setup page index.html that generates the add-in manifest for its own address, .htaccess,
  web.config, robots.txt and the downloads of the other editions listed in downloads.json (COM add-in ZIP
  dist\InventaSketch-AddIn-<version>.zip, LibreOffice .oxt files, only if they exist).
  The work is done by hosts\office-web\webspace\package.mjs (Node.js); this script only locates Node and the web build.
.PARAMETER BuildWeb
  Run npm run build in hosts\office-web first if the web build is missing.
.PARAMETER Version
  Version for the package and the download names (default: from Directory.Build.props).
#>
param([switch]$BuildWeb, [string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root 'hosts\office-web'
if (-not (Get-Command node -ErrorAction SilentlyContinue) -and (Test-Path 'C:\Program Files\nodejs\node.exe')) {
    $env:PATH = 'C:\Program Files\nodejs;' + $env:PATH
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw 'Node.js not found (https://nodejs.org).' }

if (-not (Test-Path (Join-Path $web 'dist\taskpane.html'))) {
    if (-not $BuildWeb) { throw 'hosts\office-web\dist is missing: run npm run build in hosts\office-web (or pass -BuildWeb).' }
    Push-Location $web
    try {
        if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' } }
        npm run build; if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
    } finally { Pop-Location }
}

$arguments = @((Join-Path $web 'webspace\package.mjs'), '--out', (Join-Path $root 'dist'))
if ($Version) { $arguments += @('--version', $Version) }
& node @arguments
if ($LASTEXITCODE -ne 0) { throw 'Webspace packaging failed.' }
