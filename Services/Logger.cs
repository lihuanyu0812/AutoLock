using System.Text;

namespace AutoLock.Services;

/// <summary>
/// 日志记录服务。
/// 将日志写入每日文件 AutoLock_yyyyMMdd.log，格式为 [yyyy-MM-dd HH:mm:ss] 消息内容。
/// 支持跨天自动切换日志文件，线程安全。
/// </summary>
public class Logger : IDisposable
{
    /// <summary>日志文件存储目录的绝对路径</summary>
    private readonly string _logDirectory;

    /// <summary>用于保证多线程写入安全的锁对象</summary>
    private readonly object _lock = new();

    /// <summary>当前正在写入的日志文件日期，用于检测跨天切换</summary>
    private DateTime _currentDate;

    /// <summary>当前日志文件的写入流</summary>
    private StreamWriter? _writer;

    /// <summary>标记对象是否已被释放</summary>
    private bool _disposed;

    /// <summary>
    /// 初始化日志服务。
    /// 如果日志目录不存在，将自动创建。
    /// </summary>
    /// <param name="logDirectory">
    /// 日志文件存储目录路径。
    /// 支持相对路径（相对于程序所在目录）和绝对路径。
    /// </param>
    public Logger(string logDirectory)
    {
        // 如果是相对路径，则相对于程序所在目录解析
        if (!Path.IsPathRooted(logDirectory))
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            logDirectory = Path.Combine(appDir, logDirectory);
        }

        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
        _currentDate = DateTime.Now.Date;
        EnsureWriter();
    }

    /// <summary>
    /// 写入一条日志记录。
    /// 格式为 [yyyy-MM-dd HH:mm:ss] 消息内容。
    /// 自动处理跨天日志文件切换。
    /// </summary>
    /// <param name="message">要记录的日志消息内容</param>
    public void Log(string message)
    {
        lock (_lock)
        {
            if (_disposed) return;

            // 检测是否跨天，需要切换日志文件
            var now = DateTime.Now;
            if (now.Date != _currentDate)
            {
                _currentDate = now.Date;
                CloseWriter();
                EnsureWriter();
            }

            var logLine = $"[{now:yyyy-MM-dd HH:mm:ss}] {message}";
            _writer?.WriteLine(logLine);
            _writer?.Flush();
        }
    }

    /// <summary>
    /// 确保日志写入流已初始化。
    /// 根据当前日期创建或打开对应的日志文件。
    /// </summary>
    private void EnsureWriter()
    {
        if (_writer != null) return;

        var fileName = $"AutoLock_{_currentDate:yyyyMMdd}.log";
        var filePath = Path.Combine(_logDirectory, fileName);
        _writer = new StreamWriter(filePath, append: true, encoding: Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    /// <summary>
    /// 关闭当前日志写入流并释放相关资源。
    /// </summary>
    private void CloseWriter()
    {
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>
    /// 释放日志服务占用的所有资源。
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            CloseWriter();
        }
    }
}
