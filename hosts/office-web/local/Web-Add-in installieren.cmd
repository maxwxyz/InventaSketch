@echo off
rem InventaSketch-Web fuer diesen Benutzer installieren, ohne Administratorrechte: Dateien nach %LOCALAPPDATA%\InventaSketch\Web,
rem Zertifikat fuer localhost (Windows fragt nach), Autostart, Eintrag in PowerPoint, Server starten.
rem Optionen: -OhneAutostart  -OhneManifest  -Probelauf
setlocal
for /f "tokens=2 delims=:." %%c in ('chcp') do set "ALTCP=%%c"
chcp 65001 >nul
"%~dp0Server\InventaSketch.WebHost.exe" --installieren --quelle "%~dp0." %*
set "RC=%ERRORLEVEL%"
chcp %ALTCP% >nul
echo.
pause
exit /b %RC%
