@echo off
rem Install InventaSketch-Web for this user without administrator rights: files to %LOCALAPPDATA%\InventaSketch\Web,
rem certificate for localhost (Windows asks), autostart, registration in PowerPoint, start the server.
rem Options: --no-autostart  --no-manifest  --dry-run  (German aliases: -OhneAutostart  -OhneManifest  -Probelauf)
setlocal
for /f "tokens=2 delims=:." %%c in ('chcp') do set "OLDCP=%%c"
chcp 65001 >nul
"%~dp0Server\InventaSketch.WebHost.exe" --install --source "%~dp0." %*
set "RC=%ERRORLEVEL%"
chcp %OLDCP% >nul
echo.
pause
exit /b %RC%
