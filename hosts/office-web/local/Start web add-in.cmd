@echo off
rem Start the InventaSketch-Web server (no window), also without installing. Log: %LOCALAPPDATA%\InventaSketch\webhost.log
setlocal
start "" "%~dp0Server\InventaSketch.WebHost.exe" %*
for /f "tokens=2 delims=:." %%c in ('chcp') do set "OLDCP=%%c"
chcp 65001 >nul
"%~dp0Server\InventaSketch.WebHost.exe" --status --wait 60
set "RC=%ERRORLEVEL%"
chcp %OLDCP% >nul
timeout /t 5 >nul
exit /b %RC%
