<#
.SYNOPSIS
  Builds the PowerPoint COM add-in (Release), copies it to %LOCALAPPDATA%\InventaSketch\AddIn and registers it for the
  current user. No administrator rights required. PowerPoint must be closed (files in use are otherwise locked).
#>
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'hosts\powerpoint-com\InventaSketch.AddIn\InventaSketch.AddIn.csproj'
$out = Join-Path $root 'hosts\powerpoint-com\InventaSketch.AddIn\bin\Release\net48'
$target = Join-Path $env:LOCALAPPDATA 'InventaSketch\AddIn'

if (-not $SkipBuild) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    $exe = if ($dotnet) { $dotnet.Source } else { Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe' }
    & $exe build $project -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
if (Get-Process POWERPNT -ErrorAction SilentlyContinue) { Write-Warning 'PowerPoint is running: copying may fail for loaded files. Close PowerPoint first.' }
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $out '*') $target -Recurse -Force
Get-ChildItem $target -Recurse | Unblock-File
& (Join-Path $PSScriptRoot 'register-com.ps1') -AddInPath (Join-Path $target 'InventaSketch.AddIn.dll')
