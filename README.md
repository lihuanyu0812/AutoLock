# AutoLock — Windows 自动锁定程序

AutoLock 是一个运行在 Windows 10/11 上的自动锁屏工具，基于 C# .NET 8 构建。用户登录或解锁系统后自动开始倒计时，到达指定时间后**强制锁定计算机**，无论是否有用户操作。

开发原因：小孩玩电脑游戏没有时间规划，一直盯着也不现实，所以开发了这个程序。

## 功能特性

- ⏱️ **定时锁定** — 登录/解锁后自动倒计时，超时即锁屏
- 🔄 **智能计时** — 锁定状态不计时，解锁后重新开始
- 🔔 **托盘运行** — 仅在通知区域显示图标，实时显示剩余时间
- 📝 **日志记录** — 每日生成日志文件，记录启动、锁定、解锁等关键事件
- 🚀 **开机自启** — 通过 bat 脚本一键启用/禁用

## 环境要求

- Windows 10 或 Windows 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## 快速开始

### 编译运行

```powershell
cd d:\project\AutoLock
dotnet build
dotnet run
```

程序启动后会在系统托盘（通知区域）显示盾牌图标，鼠标悬停可查看剩余锁定时间。

### 关闭程序

右键点击托盘图标 → **关闭**

## 配置说明

编辑exe程序目录下的 `appsettings.json`：

```json
{
  "LockTimeoutMinutes": 60,
  "LogDirectory": "logs"
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `LockTimeoutMinutes` | 锁定超时时间（分钟） | `60` |
| `LogDirectory` | 日志文件存储目录（支持相对/绝对路径） | `logs` |

> **注意**：修改配置后需重启程序才能生效。

### 快捷修改锁定时间

双击运行 `SetLockTimeout.bat`，按提示输入分钟数后回车即可自动更新 `appsettings.json`，无需手动编辑文件。

## 开机自启

程序目录下提供以下脚本，双击运行即可：

| 脚本 | 说明 |
|------|------|
| `enable_autostart.bat` | 启用开机自启（写入当前用户注册表） |
| `disable_autostart.bat` | 禁用开机自启（删除注册表条目） |
| `SetLockTimeout.bat` | 交互式修改锁定超时时间（自动更新配置文件） |

无需管理员权限。

## 日志

日志文件存储在配置的目录中，文件名格式为 `AutoLock_yyyyMMdd.log`。

示例内容：

```
[2026-02-17 09:00:00] 程序启动
[2026-02-17 09:00:00] 开始倒计时，锁定时间：60 分钟
[2026-02-17 10:00:00] 倒计时结束，执行锁定计算机
[2026-02-17 10:00:00] 计算机已锁定，停止计时
[2026-02-17 10:05:30] 用户解锁，重新开始倒计时
[2026-02-17 10:05:30] 开始倒计时，锁定时间：60 分钟
[2026-02-17 18:00:00] 程序结束
```

## 项目结构

```
AutoLock/
├── Program.cs               # 程序入口
├── Services/
│   ├── SessionMonitor.cs    # 会话状态监听（锁定/解锁/登录）
│   ├── LockTimer.cs         # 倒计时与自动锁定
│   ├── TrayIconManager.cs   # 系统托盘图标管理
│   └── Logger.cs            # 日志服务
├── Models/
│   └── AppSettings.cs       # 配置模型
├── appsettings.json         # 配置文件（源码目录）
├── enable_autostart.bat     # 启用开机自启
├── disable_autostart.bat    # 禁用开机自启
└── SetLockTimeout.bat       # 快捷修改锁定超时时间
```

## 技术实现

- **会话监听**：通过 `SystemEvents.SessionSwitch` 捕获锁定/解锁/登录事件
- **定时锁定**：通过 P/Invoke 调用 `user32.dll` 的 `LockWorkStation()` API
- **防止多开**：使用全局命名互斥体 `Global\AutoLock_SingleInstance`
- **托盘图标**：基于 Windows Forms 的 `NotifyIcon` + `ApplicationContext`

## 许可证

MIT License

更新记录：
2026-03-02修复：
LockTimer.cs：在OnTimerTick调用 LockWorkStation() 之前，记录主动锁定的时间戳（_lastAutoLockTime），并通过 LastAutoLockTime 属性暴露出去。
SessionMonitor.cs：在处理 SessionUnlock 事件时，先检查距最近一次程序主动锁定是否不足 30 秒。如果是，则记录日志并忽略该事件，不重启倒计时。