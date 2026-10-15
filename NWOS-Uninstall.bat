@echo off
rem NWOS deinstallieren (falls bereits installiert).
if exist "%LOCALAPPDATA%\Programs\NWOS\Uninstall-NWOS.ps1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%LOCALAPPDATA%\Programs\NWOS\Uninstall-NWOS.ps1" %*
) else (
  echo NWOS ist nicht installiert.
)
echo.
pause
