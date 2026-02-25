using Microsoft.Extensions.Configuration;
using AutoLock.Models;
using AutoLock.Services;

namespace AutoLock;

/// <summary>
/// 程序入口类。
/// 负责初始化互斥体防止多开、加载配置、创建各服务并启动应用程序主循环。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序入口方法。
    /// 执行流程：检查多开 -> 加载配置 -> 初始化服务 -> 启动主循环。
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 使用命名互斥体防止程序多开
        using var mutex = new Mutex(true, @"Global\AutoLock_SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("AutoLock 程序已在运行中。", "AutoLock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 启用视觉样式（使控件呈现现代外观）
        ApplicationConfiguration.Initialize();

        // 加载配置文件
        var config = LoadConfiguration();

        // 初始化各服务
        var logger = new Logger(config.LogDirectory);
        var lockTimer = new LockTimer(config.LockTimeoutMinutes, logger);
        var sessionMonitor = new SessionMonitor(lockTimer, logger);

        // 记录程序启动
        logger.Log("程序启动");

        // 启动会话状态监听
        sessionMonitor.Start();

        // 启动后立即开始倒计时（用户当前已登录）
        lockTimer.StartCountdown();

        // 创建托盘图标管理器并进入消息循环
        // TrayIconManager 继承 ApplicationContext，Application.Exit() 时触发其 Dispose
        using var trayIcon = new TrayIconManager(lockTimer, sessionMonitor, logger);
        Application.Run(trayIcon);
    }

    /// <summary>
    /// 从 appsettings.json 文件加载应用程序配置。
    /// 如果配置文件不存在或字段缺失，将使用默认值。
    /// </summary>
    /// <returns>包含所有配置项的 AppSettings 对象</returns>
    private static AppSettings LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        var configuration = builder.Build();
        var settings = new AppSettings();
        configuration.Bind(settings);

        return settings;
    }
}
