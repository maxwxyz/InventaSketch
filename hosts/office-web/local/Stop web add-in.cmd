@echo off
rem Stop the InventaSketch-Web server (the autostart entry stays).
setlocal
for /f "tokens=2 delims=:." %%c in ('chcp') do set "OLDCP=%%c"
chcp 65001 >nul
"%~dp0Server\InventaSketch.WebHost.exe" --stop
set "RC=%ERRORLEVEL%"
chcp %OLDCP% >nul
timeout /t 5 >nul
exit /b %RC%
