@echo off
title WFI Server Setup
powershell.exe -NoProfile -File "%~dp0scripts\Setup-Server.ps1"
if errorlevel 1 pause
