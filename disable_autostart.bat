@echo off
chcp 65001 >nul
:: ============================================
:: AutoLock 禁用开机自启脚本
:: 删除当前用户注册表中的自启条目
:: ============================================

set "APP_NAME=AutoLock"
set "REG_KEY=HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"

:: 检查注册表中是否存在该条目
reg query "%REG_KEY%" /v "%APP_NAME%" >nul 2>&1

if %errorlevel% equ 0 (
    :: 删除注册表条目
    reg delete "%REG_KEY%" /v "%APP_NAME%" /f >nul 2>&1
    if %errorlevel% equ 0 (
        echo [成功] AutoLock 开机自启已关闭。
    ) else (
        echo [错误] 关闭开机自启失败，请检查权限。
    )
) else (
    echo [提示] AutoLock 当前未设置开机自启，无需操作。
)

pause
