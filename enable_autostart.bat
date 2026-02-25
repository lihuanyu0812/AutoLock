@echo off
chcp 65001 >nul
:: ============================================
:: AutoLock 启用开机自启脚本
:: 通过当前用户注册表实现，无需管理员权限
:: ============================================

set "APP_NAME=AutoLock"
set "APP_PATH=%~dp0bin\Debug\net8.0-windows\AutoLock.exe"
set "REG_KEY=HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"

:: 检查 AutoLock.exe 是否存在
if not exist "%APP_PATH%" (
    echo [错误] 未找到 AutoLock.exe，请确保此脚本与 AutoLock.exe 在同一目录下。
    pause
    exit /b 1
)

:: 写入注册表
reg add "%REG_KEY%" /v "%APP_NAME%" /t REG_SZ /d "\"%APP_PATH%\"" /f >nul 2>&1

if %errorlevel% equ 0 (
    echo [成功] AutoLock 已设置为开机自启。
    echo 路径: %APP_PATH%
) else (
    echo [错误] 设置开机自启失败，请检查权限。
)

pause
