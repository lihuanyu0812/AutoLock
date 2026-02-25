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
    [DllImport("user32.dll")]
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

        var elapsed = DateTime.Now - _startTime;
        if (elapsed.TotalMinutes >= _timeoutMinutes)
        {
            _isRunning = false;
            StopTimer();
            _logger.Log("倒计时结束，执行锁定计算机");
            LockWorkStation();
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
