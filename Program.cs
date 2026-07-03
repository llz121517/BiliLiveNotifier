using BiliLiveNotifier.Core;

namespace BiliLiveNotifier;

class Program
{
    static async Task Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;
        LLog.Raw();
        LLog.Info("===== BiliLiveNotifier v0.1.0-Dev Started =====");

        ToastNotifier.InitListener();

        // Toast 后台激活启动 → 休眠等待
        if (args.Length > 0)
        {
            LLog.Info("[Main] 由 Toast 后台激活启动, 进入休眠等待...");
            await Task.Delay(5000);
            return;
        }

        // 1. 初始化
        ConfigManager.Initialize();
        ApiClient.LoadEndpoints();

        var config = NotifierConfig.FromJsonNode(ConfigManager.Config);
        var liveEnv = new LiveEnvironment(config);

        LLog.Info($"[Main] 初始化完成, 监控 {config.Uids.Length} 个主播\n");

        // 2. 配置热重载 → 差量更新监控实例
        ConfigManager.OnConfigReloaded += async _ =>
        {
            LLog.Info("[Reload] 检测到配置变更");
            await liveEnv.ReloadAsync();
        };

        // 3. 启动所有监控
        await liveEnv.StartAllAsync();

        // 4. 保持运行，直到 Ctrl+C
        var exitCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitCts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, exitCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C 触发
        }

        // 5. 优雅退出
        LLog.Info("[Main] 正在停止所有监控...");
        await liveEnv.StopAllAsync();
        ToastImageCache.ClearCache();
        LLog.Info("[Main] 已退出");
    }
}