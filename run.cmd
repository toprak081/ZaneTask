@echo off
rem Double-click to start ZaneTask. Runs run.ps1 without changing the system's PowerShell policy.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1"
pause
