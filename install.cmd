@echo off
rem Double-click to install (or update) the ZaneTask desktop app. Your data is kept.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install-desktop.ps1"
pause
