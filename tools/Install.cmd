@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\install.ps1"
if errorlevel 1 (
  pause
  exit /b 1
)
start "" "%USERPROFILE%\.config\vascend-engine\bin\VascendEngine.exe"
