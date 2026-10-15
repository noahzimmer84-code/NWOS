@echo off
rem NWOS installieren: baut aus dem Quellcode und richtet alles ein (ohne Administratorrechte).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-NWOS.ps1" %*
echo.
pause
