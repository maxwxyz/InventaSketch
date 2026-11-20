@echo off
rem Server von InventaSketch-Web starten (ohne Fenster), auch ohne Installation. Protokoll: %LOCALAPPDATA%\InventaSketch\webhost.log
setlocal
start "" "%~dp0Server\InventaSketch.WebHost.exe" %*
for /f "tokens=2 delims=:." %%c in ('chcp') do set "ALTCP=%%c"
chcp 65001 >nul
"%~dp0Server\InventaSketch.WebHost.exe" --status --warten 60
set "RC=%ERRORLEVEL%"
chcp %ALTCP% >nul
timeout /t 5 >nul
exit /b %RC%
