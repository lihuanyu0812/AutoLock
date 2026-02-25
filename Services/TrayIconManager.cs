namespace AutoLock.Services;

/// <summary>
/// 系统托盘图标管理器。
/// 继承 ApplicationContext 以管理应用生命周期，
/// 在通知区域显示图标，提供右键菜单用于关闭程序，
/// 动态更新图标提示文字显示剩余锁定时间。
/// </summary>
public class TrayIconManager : ApplicationContext
{
    /// <summary>系统托盘通知图标</summary>
    private readonly NotifyIcon _notifyIcon;

    /// <summary>用于定期更新提示文字的定时器</summary>
    private readonly System.Windows.Forms.Timer _updateTimer;

    /// <summary>倒计时定时器服务引用，用于获取剩余时间</summary>
    private readonly LockTimer _lockTimer;

    /// <summary>会话状态监听服务引用，退出时需要停止监听</summary>
    private readonly SessionMonitor _sessionMonitor;

    /// <summary>日志服务引用，退出时需要记录日志并释放</summary>
    private readonly Logger _logger;

    /// <summary>
    /// 初始化系统托盘图标管理器。
    /// 创建托盘图标、右键菜单，并启动提示文字更新定时器。
    /// </summary>
    /// <param name="lockTimer">倒计时定时器实例，用于获取剩余时间信息</param>
    /// <param name="sessionMonitor">会话监听服务实例，退出时需要停止</param>
    /// <param name="logger">日志服务实例，退出时记录日志并释放</param>
    public TrayIconManager(LockTimer lockTimer, SessionMonitor sessionMonitor, Logger logger)
    {
        _lockTimer = lockTimer;
        _sessionMonitor = sessionMonitor;
        _logger = logger;

        // 创建右键菜单
        var contextMenu = new ContextMenuStrip();
        var closeItem = new ToolStripMenuItem("关闭");
        closeItem.Click += OnCloseClicked;
        contextMenu.Items.Add(closeItem);

        // 创建通知区域图标
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "AutoLock",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        // 启动定时器，每秒更新一次提示文字
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _updateTimer.Tick += OnUpdateTick;
        _updateTimer.Start();
    }

    /// <summary>
    /// 定时器回调，每秒更新托盘图标的提示文字。
    /// 显示当前倒计时状态和剩余时间。
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnUpdateTick(object? sender, EventArgs e)
    {
        var remaining = _lockTimer.GetRemainingTime();
        if (_lockTimer.IsRunning)
        {
            // NotifyIcon.Text 最大长度为 127 个字符
            _notifyIcon.Text = $"AutoLock - 剩余 {(int)remaining.TotalMinutes} 分 {remaining.Seconds} 秒";
        }
        else
        {
            _notifyIcon.Text = "AutoLock - 已暂停";
        }
    }

    /// <summary>
    /// "关闭"菜单项点击事件处理方法。
    /// 按顺序执行清理操作后退出应用程序。
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnCloseClicked(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    /// <summary>
    /// 释放托盘管理器占用的所有资源。
    /// 按顺序：停止更新定时器 -> 停止倒计时 -> 停止会话监听 -> 记录日志 -> 释放图标。
    /// </summary>
    /// <param name="disposing">是否正在执行托管资源释放</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 停止更新定时器
            _updateTimer.Stop();
            _updateTimer.Dispose();

            // 停止倒计时
            _lockTimer.Dispose();

            // 停止会话监听
            _sessionMonitor.Dispose();

            // 记录程序结束日志并释放日志服务
            _logger.Log("程序结束");
            _logger.Dispose();

            // 隐藏并释放托盘图标
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
