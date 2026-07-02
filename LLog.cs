using System;
using System.IO;
using System.Linq;
using System.Reflection;

/// <summary>
/// 极简单文件日志库 (LLog)
/// 用法: LLog.Info("消息"); LLog.Level = LLog.LogLevel.Debug;
/// </summary>
public static class LLog
{
    public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }

    // ================= 配置区 (可随时修改) =================
    public static LogLevel Level { get; set; } = LogLevel.Info;
    public static string DirName = "logs";
    public static int KeepDays { get; set; } = 7;
    public static bool FlushConsole { get; set; } = true;
    // =====================================================

    private static string? _dirCache;
    public static string Dir
    {
        get
        {
            if (_dirCache == null)
            {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                _dirCache = Path.Combine(basePath, DirName);
            }
            return _dirCache;
        }
        set
        {
            _dirCache = Path.IsPathRooted(value)
                ? value
                : Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", value);
        }
    }

    private static readonly object _lock = new();
    private static DateTime _lastCleanupDate = DateTime.MinValue;

    private static void Write(LogLevel lv, string tag, string msg, bool raw = false)
    {
        if (Level > lv) return;

        var now = DateTime.Now;
        var line = raw ? msg : $"[{now:yyyy-MM-dd HH:mm:ss}] [{tag}] {msg}";

        lock (_lock)
        {
            // 1. 控制台输出
            Console.WriteLine(line);
            if (FlushConsole) Console.Out.Flush();

            try
            {
                // 2. 确保目录存在
                if (!Directory.Exists(Dir))
                    Directory.CreateDirectory(Dir);

                // 3. 追加写入当天日志
                var logFile = Path.Combine(Dir, $"{now:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, line + Environment.NewLine);

                // 4. 每天首次写入时清理过期日志 (性能优化)
                if (_lastCleanupDate.Date < now.Date)
                {
                    CleanupOldLogs(now);
                    _lastCleanupDate = now;
                }
            }
            catch (Exception ex)
            {
                // 日志系统自身异常不能抛出，仅尝试输出到控制台
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[LLog FATAL] 写入日志失败: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    private static void CleanupOldLogs(DateTime now)
    {
        try
        {
            var cutoff = now.AddDays(-KeepDays).Date;
            var files = Directory.GetFiles(Dir, "*.log");

            foreach (var file in files)
            {
                // 优先从文件名解析日期，避免频繁读取文件属性
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var fileDate))
                {
                    if (fileDate < cutoff)
                        File.Delete(file);
                }
            }
        }
        catch { /* 清理失败静默忽略 */ }
    }

    // ================= 对外API =================
    public static void Debug(string msg) => Write(LogLevel.Debug, "DEBUG", msg);
    public static void Info(string msg)  => Write(LogLevel.Info,  "INFO",  msg);
    public static void Warn(string msg)  => Write(LogLevel.Warn,  "WARN",  msg);
    public static void Error(string msg) => Write(LogLevel.Error, "ERROR", msg);
    public static void Line() => Write(LogLevel.Info, "", "", raw: true);
}