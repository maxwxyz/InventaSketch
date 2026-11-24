<#
  Helpers for the PowerPoint UI scenarios (tests/powerpoint-com/scenarios): start a fresh PowerPoint with the installed
  InventaSketch COM add-in, drive the mouse like a user (DPI aware), read the sketch models stored on the shapes, take
  screenshots and close PowerPoint again without saving. Windows PowerShell 5.1, PowerPoint desktop, add-in installed.
#>

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
if (-not ('IskTest.Native' -as [type])) {
    Add-Type -Namespace IskTest -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern uint GetDpiForSystem();
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint x, uint y, uint data, System.UIntPtr extra);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);
'@
}
[IskTest.Native]::SetProcessDPIAware() | Out-Null
$script:Screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:ProgId = 'InventaSketch.AddIn.Connect'

# ------------------------------------------------------------------------------------------------ session

function Get-IskPowerPointPath {
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\POWERPNT.EXE'
    $path = $null
    try { $path = (Get-ItemProperty $key -ErrorAction Stop).'(default)' } catch {}
    if (-not $path) { $path = 'C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE' }
    $path
}

function Get-IskApp {
    try { [Runtime.InteropServices.Marshal]::GetActiveObject('PowerPoint.Application') } catch { $null }
}

<#
.SYNOPSIS Starts PowerPoint with a blank slide for one scenario. Refuses to run while PowerPoint is open, so the
          scenarios never touch the user's presentations.
#>
function Start-IskSession {
    param([Parameter(Mandatory)][string]$Scenario, [Parameter(Mandatory)][string]$OutDir)
    if (Get-Process POWERPNT -ErrorAction SilentlyContinue) {
        $e = New-Object System.InvalidOperationException 'PowerPoint is running. Save your work and close PowerPoint first; the scenarios start and quit their own PowerPoint.'
        $e.Data['IskForeign'] = $true
        throw $e
    }
    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $started = Get-Date
    # Started through Explorer, like a user would, so the add-in loads in the normal desktop context.
    explorer.exe (Get-IskPowerPointPath)
    $app = $null
    for ($i = 0; $i -lt 240 -and -not $app; $i++) { Start-Sleep -Milliseconds 500; $app = Get-IskApp }
    if (-not $app) { throw 'PowerPoint did not start within 120 s.' }
    # Right after start PowerPoint answers COM calls before its add-in list exists (COMAddIns is null for a while).
    $connected = $false
    for ($i = 0; $i -lt 60 -and -not $connected; $i++) {
        Start-Sleep 1
        try { $connected = [bool]$app.COMAddIns.Item($script:ProgId).Connect } catch { $connected = $false }
    }
    if (-not $connected) { throw 'The InventaSketch add-in is not connected after 60 s (installed? disabled?).' }
    Start-Sleep 3
    $pres = $app.Presentations.Add(-1)
    $slide = $pres.Slides.Add(1, 12) # ppLayoutBlank
    $window = $app.ActiveWindow
    $window.WindowState = 3 # maximized
    $window.View.GotoSlide(1)
    $window.Activate()
    Start-Sleep 2
    $session = [pscustomobject]@{
        Scenario = $Scenario
        OutDir   = $OutDir
        Started  = $started
        App      = $app
        AddIn    = $app.COMAddIns.Item($script:ProgId).Object
        Slide    = $slide
        Failures = New-Object System.Collections.Generic.List[string]
        Passes   = 0
    }
    # The active window is read on every use: a stored window object goes stale after the add-in redraws shapes.
    $session | Add-Member -MemberType ScriptProperty -Name Window -Value { if ($this.App) { $this.App.ActiveWindow } else { $null } }
    $session
}

<# .SYNOPSIS Closes every presentation of the session's PowerPoint without saving, quits it and waits until it is gone. #>
function Stop-IskSession {
    param($S, [switch]$StartedHere)
    # Without a session (its start failed) only a PowerPoint this run started itself is closed.
    $app = if ($S) { $S.App } elseif ($StartedHere) { Get-IskApp } else { $null }
    if ($app) {
        try { foreach ($p in @($app.Presentations)) { $p.Saved = -1; $p.Close() } } catch {}
        try { $app.Quit() } catch {}
        try { [Runtime.InteropServices.Marshal]::ReleaseComObject($app) | Out-Null } catch {}
    }
    if ($S) { $S.App = $null; $S.AddIn = $null; $S.Slide = $null }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    # PowerPoint can take half a minute to end after Quit.
    for ($i = 0; $i -lt 180 -and (Get-Process POWERPNT -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
    -not (Get-Process POWERPNT -ErrorAction SilentlyContinue)
}

# ------------------------------------------------------------------------------------------------ checks

function Assert-Isk {
    param($S, [bool]$Condition, [string]$Message)
    if ($Condition) { $S.Passes++; Write-Host "  PASS $Message" -ForegroundColor Green }
    else { $S.Failures.Add($Message); Write-Host "  FAIL $Message" -ForegroundColor Red }
}

function Write-IskInfo { param([string]$Message) Write-Host "  $Message" }

function Get-IskDistance { param($A, $B) [Math]::Sqrt(($A[0] - $B[0]) * ($A[0] - $B[0]) + ($A[1] - $B[1]) * ($A[1] - $B[1])) }

<# .SYNOPSIS Distance of point P from the line through A and B. #>
function Get-IskLineDistance {
    param($P, $A, $B)
    $dx = $B[0] - $A[0]; $dy = $B[1] - $A[1]; $l = [Math]::Sqrt($dx * $dx + $dy * $dy)
    [Math]::Abs((($P[0] - $A[0]) * $dy - ($P[1] - $A[1]) * $dx) / $l)
}

# ------------------------------------------------------------------------------------------------ shapes and models

<# .SYNOPSIS Shapes of the slide whose name matches a wildcard pattern (InventaSketch shapes are named "ISK:<type>:<id>"). #>
function Get-IskShapes { param($S, [string]$Pattern = 'ISK:*') @($S.Slide.Shapes | Where-Object { $_.Name -like $Pattern }) }

function Get-IskSelected { param($S) $S.Window.Selection.ShapeRange.Item(1) }

function Write-IskShapes {
    param($S)
    # Not $s: PowerShell names are case-insensitive, $s would overwrite the session $S.
    foreach ($shape in $S.Slide.Shapes) { Write-IskInfo ('{0} at {1:0.0},{2:0.0} size {3:0.0}x{4:0.0}' -f $shape.Name, $shape.Left, $shape.Top, $shape.Width, $shape.Height) }
}

<# .SYNOPSIS The sketch model JSON stored on a shape (tag ISK_MODEL, gzip + base64). #>
function Get-IskModel {
    param($Shape)
    # A shape the add-in just redrew is gone: its stale reference has no tags any more.
    $v = $null
    try { $v = $Shape.Tags.Item('ISK_MODEL') } catch { return '' }
    if (-not $v) { return '' }
    $ms = New-Object IO.MemoryStream(, [Convert]::FromBase64String($v.Substring(5)))
    (New-Object IO.StreamReader((New-Object IO.Compression.GZipStream($ms, [IO.Compression.CompressionMode]::Decompress)))).ReadToEnd()
}

<# .SYNOPSIS The points (mm, page coordinates) of the first point object in a shape's model, as @(x, y) pairs. #>
function Get-IskPoints {
    param($Shape)
    $m = Get-IskModel $Shape
    $i = $m.IndexOf('"points"')
    if ($i -lt 0) { return @() }
    $seg = $m.Substring($i, $m.IndexOf(']', $i) - $i + 1)
    , @([regex]::Matches($seg, '"x":\s*([-\d.eE]+),\s*"y":\s*([-\d.eE]+)') | ForEach-Object {
            , @([double]::Parse($_.Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture), [double]::Parse($_.Groups[2].Value, [Globalization.CultureInfo]::InvariantCulture))
        })
}

function Format-IskPoints { param($Points) ($Points | ForEach-Object { '({0:0.00}; {1:0.00})' -f $_[0], $_[1] }) -join ' ' }

# ------------------------------------------------------------------------------------------------ screen

function Get-IskScale { [IskTest.Native]::GetDpiForSystem() / 96.0 }

<# .SYNOPSIS Screen rectangle (physical pixels) of a shape, via the add-in: X, Y, W, H and the centre CX, CY. #>
function Get-IskRect {
    param($S, [string]$ShapeName)
    $p = ($S.AddIn.ScreenRect($ShapeName)) -split ','
    $r = @{ X = [int]$p[0]; Y = [int]$p[1]; W = [int]$p[2]; H = [int]$p[3] }
    $r.CX = $r.X + [int]($r.W / 2); $r.CY = $r.Y + [int]($r.H / 2)
    $r
}

<# .SYNOPSIS Screen pixel of a page point in mm (model coordinates). #>
function ConvertTo-IskPixel {
    param($S, $Mm)
    @($S.Window.PointsToScreenPixelsX($Mm[0] * 72 / 25.4), $S.Window.PointsToScreenPixelsY($Mm[1] * 72 / 25.4))
}

function Save-IskShot {
    param($S, [Parameter(Mandatory)][string]$Name, [int[]]$Around)
    $b = New-Object System.Drawing.Bitmap $script:Screen.Width, $script:Screen.Height
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen(0, 0, 0, 0, $b.Size)
    $img = $b
    if ($Around) {
        $w = [Math]::Min($Around[2], $script:Screen.Width); $h = [Math]::Min($Around[3], $script:Screen.Height)
        $x = [Math]::Max(0, [Math]::Min($Around[0], $script:Screen.Width - $w)); $y = [Math]::Max(0, [Math]::Min($Around[1], $script:Screen.Height - $h))
        $img = $b.Clone((New-Object System.Drawing.Rectangle $x, $y, $w, $h), $b.PixelFormat)
    }
    $path = Join-Path $S.OutDir ($Name + '.png')
    $img.Save($path)
    if ($img -ne $b) { $img.Dispose() }
    $g.Dispose(); $b.Dispose()
    Write-IskInfo "shot $path"
}

# ------------------------------------------------------------------------------------------------ mouse and keyboard

function Move-IskMouse {
    param([double]$X, [double]$Y)
    $nx = [uint32]([Math]::Round([Math]::Max(0, $X) * 65535 / ($script:Screen.Width - 1)))
    $ny = [uint32]([Math]::Round([Math]::Max(0, $Y) * 65535 / ($script:Screen.Height - 1)))
    [IskTest.Native]::mouse_event(0x8001, $nx, $ny, 0, [UIntPtr]::Zero) # MOVE | ABSOLUTE
}

function Invoke-IskClick {
    param([double]$X, [double]$Y, [int]$WaitMs = 1500)
    Move-IskMouse ($X - 30) ($Y + 30); Start-Sleep -Milliseconds 300
    Move-IskMouse $X $Y; Start-Sleep -Milliseconds 500
    [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 100
    [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds $WaitMs
}

<#
.SYNOPSIS Presses at From, moves along a path in steps and releases. Path: $null = straight line to To; else a script
          block mapping t (0..1) to @(x, y). BeforeRelease runs while the button is still down (e.g. a screenshot).
#>
function Invoke-IskDrag {
    param([double[]]$From, [double[]]$To, [scriptblock]$Path, [int]$Steps = 14, [scriptblock]$BeforeRelease, [int]$WaitMs = 4000)
    Move-IskMouse ($From[0] + 25) ($From[1] + 25); Start-Sleep -Milliseconds 300
    Move-IskMouse $From[0] $From[1]; Start-Sleep -Milliseconds 500
    [IskTest.Native]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 150
    for ($i = 1; $i -le $Steps; $i++) {
        $t = $i / $Steps
        $p = if ($Path) { & $Path $t } else { @(($From[0] + ($To[0] - $From[0]) * $t), ($From[1] + ($To[1] - $From[1]) * $t)) }
        Move-IskMouse $p[0] $p[1]; Start-Sleep -Milliseconds 50
    }
    Start-Sleep -Milliseconds 400
    if ($BeforeRelease) { & $BeforeRelease }
    [IskTest.Native]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds $WaitMs
}

<# .SYNOPSIS Drags along a circular arc around Center (pixels) from angle Start by Sweep (radians). #>
function Invoke-IskArcDrag {
    param([double[]]$Center, [double]$Radius, [double]$Start, [double]$Sweep, [scriptblock]$BeforeRelease, [int]$Steps = 20)
    $from = @(($Center[0] + $Radius * [Math]::Cos($Start)), ($Center[1] + $Radius * [Math]::Sin($Start)))
    $path = { param($t) $a = $Start + $Sweep * $t; @(($Center[0] + $Radius * [Math]::Cos($a)), ($Center[1] + $Radius * [Math]::Sin($a))) }.GetNewClosure()
    Invoke-IskDrag -From $from -Path $path -Steps $Steps -BeforeRelease $BeforeRelease
}

function Send-IskEscape {
    [IskTest.Native]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event(0x1B, 0, 2, [UIntPtr]::Zero)
}

# ------------------------------------------------------------------------------------------------ add-in log

<#
.SYNOPSIS Path of an up-to-date copy of the add-in log. Shells with a virtualised profile (sandboxed agents) see a stale
          %LOCALAPPDATA%; then the log is copied through Explorer, which runs in the real desktop context.
#>
function Get-IskLog {
    param($S)
    $log = Join-Path $env:LOCALAPPDATA 'InventaSketch\addin.log'
    if ((Test-Path $log) -and (Get-Item $log).LastWriteTime -ge $S.Started) { return $log }
    # A fresh pair of files per call: Explorer may still hold the previous helper while the next copy is wanted.
    $n = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $copy = Join-Path $S.OutDir "addin-$n.log"
    $cmd = Join-Path $S.OutDir "copy-log-$n.cmd"
    Set-Content -Encoding ascii $cmd "@copy /y `"%LOCALAPPDATA%\InventaSketch\addin.log`" `"$copy`" >nul"
    explorer.exe $cmd
    for ($i = 0; $i -lt 40 -and -not (Test-Path $copy); $i++) { Start-Sleep -Milliseconds 250 }
    Start-Sleep -Milliseconds 300
    Remove-Item $cmd -ErrorAction SilentlyContinue
    $copy
}

<# .SYNOPSIS Button ids of the floating bar as last shown (from the add-in log). #>
function Get-IskBarIds {
    param($S)
    $log = Get-IskLog $S
    if (-not (Test-Path $log)) { return @() }
    $line = Get-Content $log | Where-Object { $_ -match 'Bar ids: ' } | Select-Object -Last 1
    if (-not $line) { return @() }
    , @(($line -replace '.*Bar ids: ', '') -split ',' | Where-Object { $_ })
}

<# .SYNOPSIS Presses Ctrl plus a key (virtual-key code). #>
function Send-IskCtrlKey {
    param([byte]$Vk)
    [IskTest.Native]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
    [IskTest.Native]::keybd_event($Vk, 0, 0, [UIntPtr]::Zero); [IskTest.Native]::keybd_event($Vk, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
    [IskTest.Native]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
}

<# .SYNOPSIS Screen rectangle of the floating bar as last shown (from the add-in log), or $null. #>
function Get-IskBarRect {
    param($S)
    $log = Get-IskLog $S
    if (-not (Test-Path $log)) { return $null }
    $line = Get-Content $log | Where-Object { $_ -match 'Bar shown at' } | Select-Object -Last 1
    $m = [regex]::Match([string]$line, 'Bar shown at (\d+),(\d+),(\d+),(\d+)')
    if (-not $m.Success) { return $null }
    @{ X = [int]$m.Groups[1].Value; Y = [int]$m.Groups[2].Value; W = [int]$m.Groups[3].Value; H = [int]$m.Groups[4].Value }
}

Export-ModuleMember -Function *-Isk*
