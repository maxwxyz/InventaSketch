<#
.SYNOPSIS
  Schreibt das Manifest des Web-Add-ins für einen eigenen HTTPS-Server (B152).
.DESCRIPTION
  Ersetzt https://localhost:3217 in manifest.xml durch die angegebene Adresse und schreibt manifest-<host>.xml. Den Ordner
  App auf den Server unter dieser Adresse legen, das Manifest im Microsoft 365 Admin Center (integrierte Apps) verteilen.
  Dieselben Regeln wie inventasketch_web.py (manifest-erzeugen.sh) und InventaSketch.WebHost.exe --manifest.
.PARAMETER Adresse
  https://host[:port][/pfad], ohne Abfrage und Anker; Schrägstriche am Ende entfallen.
.PARAMETER Quelle
  Manifest mit https://localhost:3217, Standard manifest.xml neben diesem Skript.
.PARAMETER Ziel
  Ordner für das neue Manifest, Standard der Ordner dieses Skripts.
.EXAMPLE
  .\manifest-erzeugen.ps1 -Adresse https://addins.firma.de/inventasketch
#>
param(
    [Parameter(Mandatory = $true)][string]$Adresse,
    [string]$Quelle = (Join-Path $PSScriptRoot 'manifest.xml'),
    [string]$Ziel = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

function Abbrechen([string]$text) {
    [Console]::Error.WriteLine($text)
    exit 2
}

$a = $Adresse.Trim()
if ($a -imatch '^https://') { $a = 'https://' + $a.Substring(8) }
$a = $a.TrimEnd('/')
if (-not $a.StartsWith('https://', [StringComparison]::Ordinal)) { Abbrechen 'Die Adresse muss mit https:// beginnen.' }
if ($a -cnotmatch '^https://[A-Za-z0-9.\-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~\-/%]*)?$') {
    Abbrechen 'Ungültige Adresse (erlaubt: https://host[:port][/pfad], ohne ? # Leer- oder Sonderzeichen).'
}
$rest = $a.Substring(8)
$i = $rest.IndexOf('/')
$autoritaet = if ($i -lt 0) { $rest } else { $rest.Substring(0, $i) }
$pfad = if ($i -lt 0) { '' } else { $rest.Substring($i) }
$name = $autoritaet.Split(':')[0]
if (-not $name -or $name.StartsWith('.') -or $name.EndsWith('.') -or $name.Contains('..')) { Abbrechen 'Ungültiger Hostname.' }
if ($autoritaet.Contains(':')) {
    $port = 0
    if (-not [int]::TryParse($autoritaet.Split(':')[1], [ref]$port) -or $port -lt 1 -or $port -gt 65535) { Abbrechen 'Ungültiger Port.' }
}
$autoritaet = $autoritaet.ToLowerInvariant()
$neu = 'https://' + $autoritaet + $pfad
$datei = Join-Path $Ziel ('manifest-' + (($autoritaet -replace ':', '-') -creplace '[^A-Za-z0-9.\-]', '-') + '.xml')
$text = [IO.File]::ReadAllText($Quelle, [Text.Encoding]::UTF8).Replace('https://localhost:3217', $neu)
[IO.File]::WriteAllText($datei, $text, (New-Object Text.UTF8Encoding $false))
Write-Host "Manifest für ${neu}: $datei"
