using System.Text.Json;
using System.Text.Json.Nodes;

namespace BiliLiveNotifier.Core;

/// <summary>
/// 配置管理模块 — ./data/config.json 读取/创建/热重载。
/// 返回 JsonObject，调用方直接用 GetPath / AsArray / GetValue 取值。
/// </summary>
public static class ConfigManager
{
    private const string ConfigDir = "data";
    private const string ConfigFile = "config.json";
    private const int DebounceMs = 3000;
    private const int RetryCount = 3;
    private const int RetryDelayMs = 200;

    private static readonly string DefaultJson = JsonSerializer.Serialize(new
    {
        uids = new[] { 6 },
        auto_start = false,
        check_interval = 30,
        live_check_interval = 60,
        birthday_text = true,
        skip_default_birthday = true,
        birthday_check_on_live_only = true

    }, new JsonSerializerOptions { WriteIndented = true });

    private static FileSystemWatcher? _watcher;
    private static CancellationTokenSource? _debounceCts;
    private static readonly object _lock = new();
    private static JsonObject? _config;

    public static string ConfigFilePath =>
        Path.Combine(AppContext.BaseDirectory, ConfigDir, ConfigFile);

    /// <summary>当前配置快照（只读，外部不要修改返回的对象）</summary>
    public static JsonObject? Config => _config;

    /// <summary>配置文件变更事件（去抖后触发）</summary>
    public static event Action<JsonObject>? OnConfigReloaded;

    /// <summary>初始化：创建目录 → 加载配置 → 启动文件监视</summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, ConfigDir));
        LoadFromDisk();
        StartWatching();
        LLog.Debug($"[Config] 初始化完成, 路径: {ConfigFilePath}");
    }

    /// <summary>停止文件监视</summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    /// <summary>从磁盘重载配置文件</summary>
    public static void Reload()
    {
        LoadFromDisk();
    }

    // ===================== 内部 =====================

    private static void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                LLog.Info($"[Config] 未找到配置文件，创建默认: {ConfigFilePath}");
                File.WriteAllText(ConfigFilePath, DefaultJson);
                _config = JsonNode.Parse(DefaultJson) as JsonObject;
                return;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var parsed = JsonNode.Parse(json) as JsonObject;

            if (parsed == null)
            {
                LLog.Warn("[Config] 配置文件为空，使用默认值");
                _config = JsonNode.Parse(DefaultJson) as JsonObject;
                return;
            }

            _config = parsed;
            LLog.Info($"[Config] 配置加载成功");
        }
        catch (JsonException ex)
        {
            LLog.Error($"[Config] 格式错误: {ex.Message}");
            BackupAndReset();
        }
        catch (Exception ex)
        {
            LLog.Error($"[Config] 读取失败: {ex.Message}");
            BackupAndReset();
        }
    }

    private static void BackupAndReset()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                File.Copy(ConfigFilePath, ConfigFilePath + ".bad", overwrite: true);
                LLog.Warn($"[Config] 已备份损坏文件: {ConfigFilePath}.bad");
            }
        }
        catch { }

        File.WriteAllText(ConfigFilePath, DefaultJson);
        _config = JsonNode.Parse(DefaultJson) as JsonObject;
        LLog.Info("[Config] 已重置为默认配置");
    }

    private static void StartWatching()
    {
        try
        {
            _watcher = new FileSystemWatcher(
                Path.Combine(AppContext.BaseDirectory, ConfigDir))
            {
                Filter = ConfigFile,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Error += (_, e) =>
                LLog.Warn($"[Config] 监视器异常: {e.GetException().Message}");
        }
        catch (Exception ex)
        {
            LLog.Warn($"[Config] 无法启动文件监视: {ex.Message}");
        }
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
        }

        var ct = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs, ct);
                if (ct.IsCancellationRequested) return;

                // 等文件解锁
                for (int i = 0; i < RetryCount; i++)
                {
                    try
                    {
                        using var fs = new FileStream(ConfigFilePath,
                            FileMode.Open, FileAccess.Read, FileShare.None);
                        break;
                    }
                    catch (IOException) when (i < RetryCount - 1)
                    {
                        await Task.Delay(RetryDelayMs, ct);
                    }
                }

                LoadFromDisk();
                if (_config != null)
                    OnConfigReloaded?.Invoke(_config);
                LLog.Info("[Config] 热重载完成");
            }
            catch (OperationCanceledException) { /* 被新事件取消 */ }
            catch (Exception ex)
            {
                LLog.Error($"[Config] 热重载异常: {ex.Message}");
            }
        }, ct);
    }
}
