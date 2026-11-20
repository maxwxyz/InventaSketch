@echo off
rem Server von InventaSketch-Web beenden (der Autostart bleibt eingetragen).
setlocal
for /f "tokens=2 delims=:." %%c in ('chcp') do set "ALTCP=%%c"
chcp 65001 >nul
"%~dp0Server\InventaSketch.WebHost.exe" --beenden
set "RC=%ERRORLEVEL%"
chcp %ALTCP% >nul
timeout /t 5 >nul
exit /b %RC%
