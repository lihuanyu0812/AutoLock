@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001>nul

set "APP_NAME=AutoLock"
set "REG_KEY=HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"

for %%I in ("%~dp0.") do set "BASE_DIR=%%~fI"
set "APP_PATH=%BASE_DIR%\AutoLock.exe"

if not exist "%APP_PATH%" set "APP_PATH=%BASE_DIR%\bin\Debug\net8.0-windows\AutoLock.exe"
if not exist "%APP_PATH%" set "APP_PATH=%BASE_DIR%\bin\Release\net8.0-windows\AutoLock.exe"

if not exist "%APP_PATH%" (
    echo [ERROR] AutoLock.exe not found.
    echo Put this script in the same folder as AutoLock.exe,
    echo or run it from source folder after dotnet build.
    pause
    exit /b 1
)

set "RUN_VALUE=\"%APP_PATH%\""
reg add "%REG_KEY%" /v "%APP_NAME%" /t REG_SZ /d "%RUN_VALUE%" /f>nul 2>&1

if errorlevel 1 (
    echo [ERROR] Failed to enable startup.
) else (
    echo [OK] Startup enabled.
    echo Path: %APP_PATH%
)

pause
