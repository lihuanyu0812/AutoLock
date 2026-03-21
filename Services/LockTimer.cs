using System.Runtime.InteropServices;

namespace AutoLock.Services;

/// <summary>
/// 倒计时定时器服务。
/// 负责管理锁定计算机的倒计时逻辑：从用户解锁/登录后开始计时，
/// 超过指定时间后自动调用 Windows API 锁定计算机。
/// 提供剩余时间查询接口供托盘图标显示。
/// </summary>
public class LockTimer : IDisposable
{
    /// <summary>
    /// 调用 Windows API 锁定工作站。
    /// 该函数无需管理员权限即可调用。
    /// </summary>
    /// <returns>如果函数成功，返回非零值</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    /// <summary>锁定超时时间（分钟）</summary>
    private readonly int _timeoutMinutes;

    /// <summary>日志服务引用</summary>
    private readonly Logger _logger;

    /// <summary>定时器，每秒触发一次检查</summary>
    private System.Threading.Timer? _timer;

    /// <summary>倒计时开始的时间点</summary>
    private DateTime _startTime;

    /// <summary>标记倒计时是否正在运行</summary>
    private volatile bool _isRunning;

    /// <summary>下次允许尝试锁定的时间点</summary>
    private DateTime _nextLockAttemptTime = DateTime.MinValue;

    /// <summary>下次输出心跳日志的时间点</summary>
    private DateTime _nextHeartbeatTime = DateTime.MinValue;

    /// <summary>程序最近一次主动触发锁定的时间点（UTC），用于过滤系统误报的解锁事件</summary>
    private DateTime _lastAutoLockTime = DateTime.MinValue;

    /// <summary>自动锁定后是否仍在等待系统上报 SessionLock 事件</summary>
    private bool _awaitingSessionLockAfterAutoLock;

    /// <summary>自动锁定相关状态访问锁</summary>
    private readonly object _autoLockStateLock = new();

    /// <summary>会话状态与错误码访问锁</summary>
    private readonly object _statusLock = new();

    /// <summary>当前会话状态文本</summary>
    private string _sessionState = "未知";

    /// <summary>最近一次锁定失败错误码</summary>
    private int? _lastLockErrorCode;

    /// <summary>
    /// 获取程序最近一次主动触发锁定的时间（UTC）。
    /// SessionMonitor 用此值判断解锁事件是否为系统在锁定后立即误发。
    /// </summary>
    public DateTime LastAutoLockTime => _lastAutoLockTime;

    /// <summary>标记对象是否已被释放</summary>
    private bool _disposed;

    /// <summary>
    /// 初始化倒计时定时器。
    /// </summary>
    /// <param name="timeoutMinutes">锁定超时时间（分钟），超过此时间自动锁定计算机</param>
    /// <param name="logger">日志服务实例，用于记录倒计时相关事件</param>
    public LockTimer(int timeoutMinutes, Logger logger)
    {
        _timeoutMinutes = timeoutMinutes;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前倒计时是否正在运行。
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 获取剩余时间。
    /// 如果倒计时未运行，返回总超时时间。
    /// </summary>
    /// <returns>剩余的 TimeSpan 时间，最小为 TimeSpan.Zero</returns>
    public TimeSpan GetRemainingTime()
    {
        if (!_isRunning)
            return TimeSpan.FromMinutes(_timeoutMinutes);

        var elapsed = DateTime.Now - _startTime;
        var remaining = TimeSpan.FromMinutes(_timeoutMinutes) - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// 开始或重置倒计时。
    /// 每次调用都会重新开始计时，之前的倒计时将被取消。
    /// 记录日志并启动每秒一次的定时检查。
    /// </summary>
    public void StartCountdown()
    {
        StopTimer();
        _startTime = DateTime.Now;
        _nextLockAttemptTime = _startTime.AddMinutes(_timeoutMinutes);
        _nextHeartbeatTime = _startTime.AddMinutes(1);
        _isRunning = true;
        _logger.Log($"开始倒计时，锁定时间：{_timeoutMinutes} 分钟");

        // 每秒检查一次是否超时
        _timer = new System.Threading.Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 停止倒计时（系统锁定时调用）。
    /// 停止定时器并记录日志。
    /// </summary>
    public void StopCountdown()
    {
        if (_isRunning)
        {
            _isRunning = false;
            StopTimer();
            _logger.Log("计算机已锁定，停止计时");
        }
    }

    /// <summary>
    /// 定时器回调函数，每秒执行一次。
    /// 检查是否已超过指定时间，如果超时则锁定计算机。
    /// </summary>
    /// <param name="state">定时器状态对象（未使用）</param>
    private void OnTimerTick(object? state)
    {
        if (!_isRunning) return;

        var now = DateTime.Now;
        WriteHeartbeatIfNeeded(now);

        if (now < _nextLockAttemptTime)
        {
            return;
        }

        var elapsed = now - _startTime;
        if (elapsed.TotalMinutes >= _timeoutMinutes)
        {
            if (TryLockWorkStation(out var errorCode))
            {
                _isRunning = false;
                StopTimer();
                lock (_statusLock)
                {
                    _lastLockErrorCode = null;
                }
                _logger.Log("倒计时结束，已发起锁定计算机请求");
                lock (_autoLockStateLock)
                {
                    _lastAutoLockTime = DateTime.UtcNow;
                    _awaitingSessionLockAfterAutoLock = true;
                }
                return;
            }

            _nextLockAttemptTime = now.AddSeconds(10);
            lock (_statusLock)
            {
                _lastLockErrorCode = errorCode;
            }
            _logger.Log($"倒计时结束，锁定请求失败（错误码：{errorCode}），10 秒后重试");
        }
    }

    /// <summary>
    /// 更新当前会话状态。
    /// </summary>
    /// <param name="sessionState">会话状态文本</param>
    /// <returns>无返回值</returns>
    public void UpdateSessionState(string sessionState)
    {
        lock (_statusLock)
        {
            _sessionState = sessionState;
        }
    }

    /// <summary>
    /// 按分钟输出倒计时心跳日志。
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <returns>无返回值</returns>
    private void WriteHeartbeatIfNeeded(DateTime now)
    {
        if (now < _nextHeartbeatTime)
        {
            return;
        }

        while (_nextHeartbeatTime <= now)
        {
            _nextHeartbeatTime = _nextHeartbeatTime.AddMinutes(1);
        }

        var remaining = TimeSpan.FromMinutes(_timeoutMinutes) - (now - _startTime);
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        string sessionState;
        int? lastLockErrorCode;
        lock (_statusLock)
        {
            sessionState = _sessionState;
            lastLockErrorCode = _lastLockErrorCode;
        }

        var errorText = lastLockErrorCode?.ToString() ?? "无";
        _logger.Log($"倒计时心跳，剩余时间：{(int)remaining.TotalMinutes} 分 {remaining.Seconds} 秒，会话状态：{sessionState}，最近锁定错误码：{errorText}");
    }

    /// <summary>
    /// 尝试调用系统 API 锁定工作站。
    /// </summary>
    /// <param name="errorCode">失败时返回系统错误码，成功时为 0</param>
    /// <returns>调用成功返回 true，否则返回 false</returns>
    private static bool TryLockWorkStation(out int errorCode)
    {
        if (LockWorkStation())
        {
            errorCode = 0;
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        return false;
    }

    /// <summary>
    /// 判断当前解锁事件是否应被识别为自动锁定后的系统误报。
    /// 仅在“刚触发自动锁定且尚未收到 SessionLock”这一窗口内返回 true。
    /// </summary>
    /// <returns>如果应忽略当前解锁事件则返回 true，否则返回 false</returns>
    public bool ShouldIgnoreUnlockEvent()
    {
        lock (_autoLockStateLock)
        {
            if (!_awaitingSessionLockAfterAutoLock)
            {
                return false;
            }

            var elapsedSeconds = (DateTime.UtcNow - _lastAutoLockTime).TotalSeconds;
            if (elapsedSeconds > 30)
            {
                _awaitingSessionLockAfterAutoLock = false;
                return false;
            }

            return elapsedSeconds >= 0;
        }
    }

    /// <summary>
    /// 通知定时器已收到系统的 SessionLock 事件。
    /// 收到后会关闭“自动锁定误报过滤窗口”，后续解锁事件将正常触发倒计时。
    /// </summary>
    public void NotifySessionLocked()
    {
        lock (_autoLockStateLock)
        {
            _awaitingSessionLockAfterAutoLock = false;
        }
    }

    /// <summary>
    /// 停止并释放内部定时器。
    /// </summary>
    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// 释放定时器服务占用的所有资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isRunning = false;
        StopTimer();
    }
}
