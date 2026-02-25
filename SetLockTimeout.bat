@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "CONFIG_FILE=d:\project\AutoLock\bin\Debug\net8.0-windows\appsettings.json"

:: 提示用户输入锁定时间
set /p INPUT_VALUE=请输入锁定时间（分钟）：

:: 验证输入是否为数字
echo %INPUT_VALUE%| findstr /r "^[0-9][0-9]*$" >nul
if %errorlevel% neq 0 (
    echo 错误：请输入有效的数字！
    pause
    exit /b 1
)

:: 检查配置文件是否存在
if not exist "%CONFIG_FILE%" (
    echo 错误：配置文件不存在：%CONFIG_FILE%
    pause
    exit /b 1
)

:: 使用 PowerShell 更新 JSON 文件中的 LockTimeoutMinutes 值
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$filePath = 'd:\project\AutoLock\bin\Debug\net8.0-windows\appsettings.json';" ^
    "$value = %INPUT_VALUE%;" ^
    "$json = Get-Content -Path $filePath -Raw -Encoding UTF8 | ConvertFrom-Json;" ^
    "$json.LockTimeoutMinutes = $value;" ^
    "$json | ConvertTo-Json -Depth 10 | Set-Content -Path $filePath -Encoding UTF8;"

if %errorlevel% equ 0 (
    echo 成功：LockTimeoutMinutes 已更新为 %INPUT_VALUE% 分钟。
) else (
    echo 错误：更新配置文件失败，请检查文件格式或权限。
)

pause
endlocal
