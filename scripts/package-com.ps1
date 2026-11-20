<#
.SYNOPSIS
  Builds the installable PowerPoint COM add-in package dist\InventaSketch-AddIn and a versioned ZIP.
.DESCRIPTION
  Layout:
    AddIn\                       add-in binaries (Release, net48) incl. App\ (sidebar editor + WebAssembly engine)
    Install.cmd             per-user install without administrator rights (double-click)
    Uninstall.cmd           remove the add-in for the current user
    install.ps1, register-com.ps1, unregister-com.ps1
    LICENSE, README.txt
  The sidebar needs the built web UI (hosts\office-web\dist with sidebar.html); build it first (npm run build) or the
  package warns and the sidebar shows a hint.
.PARAMETER Version
  Version for the ZIP name (default: from Directory.Build.props).
#>
param([string]$Version = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = $null
foreach ($c in @((Get-Command dotnet -ErrorAction SilentlyContinue).Source, (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'))) {
    if ($c -and (Test-Path $c) -and ((& $c --list-sdks 2>$null) -match '^10\.')) { $dotnet = $c; break }
}
if (-not $dotnet) { throw 'No .NET 10 SDK found; run scripts\build.ps1 once (installs it user-locally).' }
if (-not $Version) { $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup[0].Version }

$project = Join-Path $root 'hosts\powerpoint-com\InventaSketch.AddIn\InventaSketch.AddIn.csproj'
& $dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'COM add-in build failed.' }

$out = Join-Path $root 'dist\InventaSketch-AddIn'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$addin = Join-Path $out 'AddIn'
New-Item -ItemType Directory -Force $addin | Out-Null
Copy-Item (Join-Path $root 'hosts\powerpoint-com\InventaSketch.AddIn\bin\Release\net48\*') $addin -Recurse -Force
Get-ChildItem $addin -Recurse -Filter *.pdb | Remove-Item
Get-ChildItem $addin -Recurse -Filter *.xml | Where-Object { $_.Name -like 'InventaSketch.*.xml' } | Remove-Item
$web = Join-Path $root 'hosts\office-web\dist'
if (Test-Path (Join-Path $web 'sidebar.html')) {
    Copy-Item $web (Join-Path $addin 'App') -Recurse -Force
    Remove-Item (Join-Path $addin 'App\manifest.xml'), (Join-Path $addin 'App\dev.html') -ErrorAction SilentlyContinue
} else {
    Write-Warning 'hosts\office-web\dist\sidebar.html missing: the sidebar editor will show a hint. Build the web UI first.'
}
foreach ($f in 'register-com.ps1', 'unregister-com.ps1') { Copy-Item (Join-Path $PSScriptRoot $f) $out }
Copy-Item (Join-Path $root 'LICENSE') $out

$install = @'
<#
  Installs the InventaSketch PowerPoint add-in for the current user (no administrator rights):
  copies AddIn\ to %LOCALAPPDATA%\InventaSketch\AddIn and registers it (HKCU). Close PowerPoint first.
#>
$ErrorActionPreference = 'Stop'
$here = Split-Path $MyInvocation.MyCommand.Path -Parent
$target = Join-Path $env:LOCALAPPDATA 'InventaSketch\AddIn'
$de = (Get-UICulture).TwoLetterISOLanguageName -eq 'de'
function T([string]$en, [string]$deText) { if ($de) { $deText } else { $en } }
if (Get-Process POWERPNT -ErrorAction SilentlyContinue) { Write-Warning (T 'PowerPoint is running. Please close PowerPoint and run the installer again.' 'PowerPoint läuft. Bitte PowerPoint schließen und die Installation erneut starten.'); exit 1 }
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $here 'AddIn\*') $target -Recurse -Force
Get-ChildItem $target -Recurse | Unblock-File
& (Join-Path $here 'register-com.ps1') -AddInPath (Join-Path $target 'InventaSketch.AddIn.dll')
Write-Host ''
Write-Host (T 'InventaSketch is installed. Start PowerPoint: tab "InventaSketch".' 'InventaSketch ist installiert. PowerPoint starten: Register "InventaSketch".')
'@
Set-Content -Path (Join-Path $out 'install.ps1') -Value $install -Encoding UTF8

$cmdInstall = "@echo off`r`nrem Installs InventaSketch for PowerPoint for the current user (no administrator rights). Close PowerPoint first.`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`"`r`necho.`r`npause`r`n"
$cmdRemove = "@echo off`r`nrem Removes InventaSketch from PowerPoint for the current user.`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0unregister-com.ps1`"`r`nif exist `"%LOCALAPPDATA%\InventaSketch\AddIn`" rmdir /s /q `"%LOCALAPPDATA%\InventaSketch\AddIn`"`r`necho.`r`npause`r`n"
[IO.File]::WriteAllText((Join-Path $out 'Install.cmd'), $cmdInstall)
[IO.File]::WriteAllText((Join-Path $out 'Uninstall.cmd'), $cmdRemove)
$readme = "InventaSketch $Version - PowerPoint add-in (Windows)`r`n`r`nInstall: close PowerPoint, double-click Install.cmd (no administrator rights needed).`r`nUninstall: Uninstall.cmd.`r`nLog: %LOCALAPPDATA%\InventaSketch\addin.log`r`nSource and documentation: https://github.com/maxwxyz/InventaSketch`r`n`r`nDeutsch: PowerPoint schliessen, Install.cmd doppelklicken (keine Administratorrechte noetig).`r`nEntfernen: Uninstall.cmd doppelklicken.`r`n"
[IO.File]::WriteAllText((Join-Path $out 'README.txt'), $readme)

$zip = Join-Path $root "dist\InventaSketch-AddIn-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "Package: $out"
Write-Host "ZIP:     $zip"
