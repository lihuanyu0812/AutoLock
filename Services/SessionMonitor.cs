using Microsoft.Win32;

namespace AutoLock.Services;

/// <summary>
/// Windows 会话状态监听服务。
/// 监听系统的锁定/解锁/登录/注销事件，
/// 在用户解锁或登录时启动倒计时，在锁定或注销时停止倒计时。
/// </summary>
public class SessionMonitor : IDisposable
{
    /// <summary>倒计时定时器服务引用</summary>
    private readonly LockTimer _lockTimer;

    /// <summary>日志服务引用</summary>
    private readonly Logger _logger;

    /// <summary>标记对象是否已被释放</summary>
    private bool _disposed;

    /// <summary>
    /// 初始化会话状态监听服务。
    /// </summary>
    /// <param name="lockTimer">倒计时定时器实例，用于控制倒计时的启停</param>
    /// <param name="logger">日志服务实例，用于记录会话事件</param>
    public SessionMonitor(LockTimer lockTimer, Logger logger)
    {
        _lockTimer = lockTimer;
        _logger = logger;
    }

    /// <summary>
    /// 开始监听 Windows 会话状态变化事件。
    /// 注册 SystemEvents.SessionSwitch 事件处理器。
    /// </summary>
    public void Start()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>
    /// 停止监听 Windows 会话状态变化事件。
    /// 注销 SystemEvents.SessionSwitch 事件处理器。
    /// </summary>
    public void Stop()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    /// <summary>
    /// 会话状态变化事件处理方法。
    /// 根据不同的会话切换原因执行相应操作：
    /// - SessionUnlock / SessionLogon：记录日志并启动倒计时
    /// - SessionLock / SessionLogoff：停止倒计时并记录日志
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">包含会话切换原因的事件参数</param>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionUnlock:
                _logger.Log("用户解锁，重新开始倒计时");
                _lockTimer.StartCountdown();
                break;

            case SessionSwitchReason.SessionLogon:
                _logger.Log("用户登录，开始倒计时");
                _lockTimer.StartCountdown();
                break;

            case SessionSwitchReason.SessionLock:
                _lockTimer.StopCountdown();
                break;

            case SessionSwitchReason.SessionLogoff:
                _lockTimer.StopCountdown();
                _logger.Log("用户注销");
                break;
        }
    }

    /// <summary>
    /// 释放会话监听服务占用的所有资源。
    /// 停止事件监听。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
