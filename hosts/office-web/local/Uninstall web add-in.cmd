@echo off
rem Remove InventaSketch-Web for this user: server, autostart, registration in PowerPoint, certificate, folder
rem %LOCALAPPDATA%\InventaSketch\Web. Runs from the package or from the installed folder. Option: --dry-run (-Probelauf)
setlocal EnableDelayedExpansion
set "EXE=%TEMP%\InventaSketch.WebHost-%RANDOM%%RANDOM%.exe"
copy /y "%~dp0Server\InventaSketch.WebHost.exe" "%EXE%" >nul || (echo Server\InventaSketch.WebHost.exe is missing / fehlt. & pause & exit /b 1)
if exist "%~dp0Server\InventaSketch.WebHost.exe.config" copy /y "%~dp0Server\InventaSketch.WebHost.exe.config" "%EXE%.config" >nul
cd /d "%TEMP%"
rem One block: cmd reads it completely before this file is deleted together with the folder.
(
  for /f "tokens=2 delims=:." %%c in ('chcp') do set "OLDCP=%%c"
  chcp 65001 >nul
  "%EXE%" --uninstall %*
  set "RC=!ERRORLEVEL!"
  chcp !OLDCP! >nul
  del "%EXE%" "%EXE%.config" >nul 2>nul
  echo.
  pause
  exit /b !RC!
)
