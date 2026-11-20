<#
.SYNOPSIS
  Removes the InventaSketch COM add-in registration (current user by default, -Scope Machine for all users).
#>
param([ValidateSet('User', 'Machine')][string]$Scope = 'User')

$ErrorActionPreference = 'Stop'
$hive = if ($Scope -eq 'Machine') { 'HKLM:' } else { 'HKCU:' }
$clsid = '{1D58D258-A932-4753-9EAA-309C5736FE34}'
$progId = 'InventaSketch.AddIn.Connect'
$paths = @(
    "$hive\Software\Classes\$progId",
    "$hive\Software\Classes\CLSID\$clsid",
    "$hive\Software\Classes\WOW6432Node\CLSID\$clsid",
    "$hive\Software\Microsoft\Office\PowerPoint\Addins\$progId",
    "$hive\Software\WOW6432Node\Microsoft\Office\PowerPoint\Addins\$progId"
)
foreach ($p in $paths) { if (Test-Path $p) { Remove-Item $p -Recurse -Force } }
Write-Host "Unregistered ($Scope). Restart PowerPoint."
