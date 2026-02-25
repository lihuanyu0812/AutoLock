namespace AutoLock.Models;

/// <summary>
/// 应用程序配置模型。
/// 用于映射 appsettings.json 中的配置项。
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 锁定超时时间（分钟）。
    /// 用户解锁/登录后经过此时间将自动锁定计算机。
    /// 默认值：60 分钟。
    /// </summary>
    public int LockTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// 日志文件存储目录。
    /// 支持相对路径（相对于程序所在目录）和绝对路径。
    /// 默认值："logs"。
    /// </summary>
    public string LogDirectory { get; set; } = "logs";
}
