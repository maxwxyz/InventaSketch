<#
.SYNOPSIS
  Registers the InventaSketch COM add-in in PowerPoint for the current user (default, no administrator rights) or,
  with -Scope Machine, for all users.
.DESCRIPTION
  Writes the COM registration under HKCU\Software\Classes (or HKLM) for 64-bit and 32-bit Office and the add-in entry
  under ...\Software\Microsoft\Office\PowerPoint\Addins. Removes Office's automatic disabling for this add-in.
  (Ported from Slidemax with the copyright holder's approval.)
.PARAMETER AddInPath
  Full path to InventaSketch.AddIn.dll.
.PARAMETER Scope
  User (HKCU, default) or Machine (HKLM, requires administrator rights).
.EXAMPLE
  .\register-com.ps1 -AddInPath "$env:LOCALAPPDATA\InventaSketch\AddIn\InventaSketch.AddIn.dll"
#>
param(
    [Parameter(Mandatory = $true)][string]$AddInPath,
    [ValidateSet('User', 'Machine')][string]$Scope = 'User',
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
function Say([string]$text) { if (-not $Quiet) { Write-Host $text } }

if (-not [Environment]::Is64BitProcess) {
    Write-Warning 'Running in 32-bit PowerShell. Use 64-bit PowerShell so the keys land in the right registry view.'
}

$AddInPath = (Resolve-Path $AddInPath).Path
$hive = if ($Scope -eq 'Machine') { 'HKLM:' } else { 'HKCU:' }
$classes = "$hive\Software\Classes"
$officeRoots = if ($Scope -eq 'Machine') { @("$hive\Software\Microsoft\Office", "$hive\Software\WOW6432Node\Microsoft\Office") } else { @("$hive\Software\Microsoft\Office") }

function Set-Key([string]$path, [hashtable]$values) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
    foreach ($name in $values.Keys) {
        $value = $values[$name]
        if ($name -eq '') { Set-Item -Path $path -Value $value }
        elseif ($value -is [int]) { New-ItemProperty -Path $path -Name $name -Value $value -PropertyType DWord -Force | Out-Null }
        else { New-ItemProperty -Path $path -Name $name -Value $value -PropertyType String -Force | Out-Null }
    }
}

function Register-ComClass([string]$path, [string]$clsid, [string]$progId, [string]$className) {
    $asm = [System.Reflection.AssemblyName]::GetAssemblyName($path)
    # CodeBase like regasm /codebase: "file:///" + path with forward slashes, NOT percent-encoded (spaces, umlauts, '#').
    $codeBase = if ($path.StartsWith('\\')) { 'file:' + $path.Replace('\', '/') } else { 'file:///' + $path.Replace('\', '/') }
    Set-Key "$classes\$progId" @{ '' = $className }
    Set-Key "$classes\$progId\CLSID" @{ '' = $clsid }
    foreach ($clsidRoot in @("$classes\CLSID", "$classes\WOW6432Node\CLSID")) {
        if (Test-Path "$clsidRoot\$clsid") { Remove-Item "$clsidRoot\$clsid" -Recurse -Force }
        Set-Key "$clsidRoot\$clsid" @{ '' = $className }
        Set-Key "$clsidRoot\$clsid\ProgId" @{ '' = $progId }
        Set-Key "$clsidRoot\$clsid\Implemented Categories\{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}" @{}
        $inproc = @{ '' = 'mscoree.dll'; 'ThreadingModel' = 'Both'; 'Class' = $className; 'Assembly' = $asm.FullName; 'RuntimeVersion' = 'v4.0.30319'; 'CodeBase' = $codeBase }
        Set-Key "$clsidRoot\$clsid\InprocServer32" $inproc
        Set-Key "$clsidRoot\$clsid\InprocServer32\$($asm.Version)" @{ 'Class' = $className; 'Assembly' = $asm.FullName; 'RuntimeVersion' = 'v4.0.30319'; 'CodeBase' = $codeBase }
    }
}

$clsid = '{1D58D258-A932-4753-9EAA-309C5736FE34}'
$progId = 'InventaSketch.AddIn.Connect'
Register-ComClass $AddInPath $clsid $progId 'InventaSketch.AddIn.Connect'
foreach ($office in $officeRoots) {
    Set-Key "$office\PowerPoint\Addins\$progId" @{
        'FriendlyName' = 'InventaSketch'
        'Description'  = 'Quick technical sketches and patent drawings'
        'LoadBehavior' = 3
    }
}

# Never let Office auto-disable the add-in as slow, and clear a hard disable after a crash.
$res = 'HKCU:\Software\Microsoft\Office\16.0\PowerPoint\Resiliency'
Set-Key "$res\DoNotDisableAddinList" @{ $progId = 1 }
if (Test-Path "$res\DisabledItems") {
    $item = Get-Item "$res\DisabledItems"
    foreach ($name in $item.GetValueNames()) {
        $data = $item.GetValue($name)
        $text = if ($data -is [byte[]]) { [Text.Encoding]::Unicode.GetString($data) } else { "$data" }
        if ($text -match '(?i)inventasketch') { Remove-ItemProperty -Path "$res\DisabledItems" -Name $name; Say 'Cleared Office auto-disable entry.' }
    }
}
if (Test-Path "$res\CrashingAddinList") { Remove-ItemProperty -Path "$res\CrashingAddinList" -Name $progId -ErrorAction SilentlyContinue }

Say "Registered ($Scope): $AddInPath"
Say "Restart PowerPoint. Log: $env:LOCALAPPDATA\InventaSketch\addin.log"
