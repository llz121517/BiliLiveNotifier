using BiliLiveNotifier.Core;

namespace BiliLiveNotifier;

class Program
{
    static async Task Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;
        LLog.Line();
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
        var appCtx = new LiveEnvironment(config);

        LLog.Info($"[Main] 初始化完成, 监控 {config.Uids.Length} 个主播\n");

        // 2. 配置热重载 → ReBoot
        ConfigManager.OnConfigReloaded += _ =>
        {
            LLog.Info("[Reload] 检测到配置变更，准备热重载...");
            string? exePath = Environment.ProcessPath;
            LLog.Debug($"[Reload] 正在 Restart: {exePath}");

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                });
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LLog.Error($"[Reload] 启动新进程失败: {ex.Message}");
            }
        };

        // 3. 创建并启动所有监控实例
        var monitors = config.Uids
            .Select(uid => new LiveMonitor(uid, appCtx))
            .ToArray();

        await Task.WhenAll(monitors.Select(m => m.StartAsync()));

        ToastImageCache.ClearCache();
    }
}