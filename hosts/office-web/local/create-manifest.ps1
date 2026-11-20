<#
.SYNOPSIS
  Writes the web add-in manifest for your own HTTPS server.
.DESCRIPTION
  Replaces https://localhost:3217 in manifest.xml with the given address and writes manifest-<host>.xml. Put the folder
  App on the server under this address and deploy the manifest in the Microsoft 365 admin center (Integrated apps).
  Same rules as inventasketch_web.py (create-manifest.sh) and InventaSketch.WebHost.exe --manifest.
  Messages are German if the Windows UI language is German, otherwise English.
.PARAMETER Address
  https://host[:port][/path], without query and fragment; trailing slashes are dropped. German alias: -Adresse.
.PARAMETER Source
  Manifest with https://localhost:3217, default manifest.xml next to this script. German alias: -Quelle.
.PARAMETER Target
  Folder for the new manifest, default the folder of this script. German alias: -Ziel.
.EXAMPLE
  .\create-manifest.ps1 -Address https://addins.example.com/inventasketch
#>
param(
    [Parameter(Mandatory = $true)][Alias('Adresse')][string]$Address,
    [Alias('Quelle')][string]$Source = (Join-Path $PSScriptRoot 'manifest.xml'),
    [Alias('Ziel')][string]$Target = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
$german = [Globalization.CultureInfo]::CurrentUICulture.TwoLetterISOLanguageName -eq 'de'

function T([string]$en, [string]$de) { if ($german) { $de } else { $en } }

function Stop-WithError([string]$text) {
    [Console]::Error.WriteLine($text)
    exit 2
}

$a = $Address.Trim()
if ($a -imatch '^https://') { $a = 'https://' + $a.Substring(8) }
$a = $a.TrimEnd('/')
if (-not $a.StartsWith('https://', [StringComparison]::Ordinal)) {
    Stop-WithError (T 'The address must start with https://.' 'Die Adresse muss mit https:// beginnen.')
}
if ($a -cnotmatch '^https://[A-Za-z0-9.\-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~\-/%]*)?$') {
    Stop-WithError (T 'Invalid address (allowed: https://host[:port][/path], without ? # spaces or special characters).' `
        'Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen).')
}
$rest = $a.Substring(8)
$i = $rest.IndexOf('/')
$authority = if ($i -lt 0) { $rest } else { $rest.Substring(0, $i) }
$path = if ($i -lt 0) { '' } else { $rest.Substring($i) }
$hostName = $authority.Split(':')[0]
if (-not $hostName -or $hostName.StartsWith('.') -or $hostName.EndsWith('.') -or $hostName.Contains('..')) {
    Stop-WithError (T 'Invalid host name.' 'Ungültiger Hostname.')
}
if ($authority.Contains(':')) {
    $port = 0
    if (-not [int]::TryParse($authority.Split(':')[1], [ref]$port) -or $port -lt 1 -or $port -gt 65535) {
        Stop-WithError (T 'Invalid port.' 'Ungültiger Port.')
    }
}
$authority = $authority.ToLowerInvariant()
$newAddress = 'https://' + $authority + $path
$file = Join-Path $Target ('manifest-' + (($authority -replace ':', '-') -creplace '[^A-Za-z0-9.\-]', '-') + '.xml')
$text = [IO.File]::ReadAllText($Source, [Text.Encoding]::UTF8).Replace('https://localhost:3217', $newAddress)
[IO.File]::WriteAllText($file, $text, (New-Object Text.UTF8Encoding $false))
Write-Host ((T 'Manifest for ' 'Manifest für ') + "${newAddress}: $file")
