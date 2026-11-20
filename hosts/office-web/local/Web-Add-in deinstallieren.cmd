@echo off
rem InventaSketch-Web fuer diesen Benutzer entfernen: Server, Autostart, Eintrag in PowerPoint, Zertifikat, Ordner
rem %LOCALAPPDATA%\InventaSketch\Web. Laeuft aus dem Paket oder aus dem installierten Ordner. Option: -Probelauf
setlocal EnableDelayedExpansion
set "EXE=%TEMP%\InventaSketch.WebHost-%RANDOM%%RANDOM%.exe"
copy /y "%~dp0Server\InventaSketch.WebHost.exe" "%EXE%" >nul || (echo Server\InventaSketch.WebHost.exe fehlt. & pause & exit /b 1)
if exist "%~dp0Server\InventaSketch.WebHost.exe.config" copy /y "%~dp0Server\InventaSketch.WebHost.exe.config" "%EXE%.config" >nul
cd /d "%TEMP%"
rem Ein Block: cmd liest ihn ganz, bevor diese Datei mit dem Ordner geloescht wird.
(
  for /f "tokens=2 delims=:." %%c in ('chcp') do set "ALTCP=%%c"
  chcp 65001 >nul
  "%EXE%" --deinstallieren %*
  set "RC=!ERRORLEVEL!"
  chcp !ALTCP! >nul
  del "%EXE%" "%EXE%.config" >nul 2>nul
  echo.
  pause
  exit /b !RC!
)
