using BiliLiveNotifier.Core;

namespace BiliLiveNotifier;

class Program
{
    public static int Interval { get; set; }
    public static bool IsToast { get; set; } = false;
    static async Task Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;
        LLog.Info(new string('=', 20));
        LLog.Info("[Boot] BiliLiveNotifier 开始运行");

        ToastNotifier.InitListener();

        if (args.Length > 0)
        {
            LLog.Info("[Boot] 由 Toast 后台激活启动, 进入休眠等待...");
            await Task.Delay(5000);
            return;
        }

        // 1. Init
        ConfigManager.Initialize();
        ApiClient.LoadEndpoints();

        var cfg = ConfigManager.Config;
        long[] uids = cfg?["Uids"]?.AsArray()
            .Select(v => (long)v!)
            .ToArray() ?? [];
        Interval = cfg?["check_interval"]?.GetValue<int>() ?? 15000;
        bool autoStart = cfg?["auto_start"]?.GetValue<bool>() ?? false;

        await Program.MonitorService(uids[0]);

        ToastImageCache.ClearCache();
    }
    static async Task MonitorService(long Uid)
    {
        var data = await ApiClient.RequestAsync("GetMasterInfo", Uid);
        long? roomId = data?.GetPath("room_id")?.GetValue<long>();

        if (!roomId.HasValue)
        {
            LLog.Warn($"[Init] UID:{Uid} 解析直播间ID失败, 跳过通知发送");
            return;
        }

        LLog.Info($"[Init] UID:{Uid} 解析到直播间ID: {roomId}");

        while (true)
        {
            data = await ApiClient.RequestAsync("GetLiveRoomDetail", roomId);
            long? live_status = data?.GetPath("live_status")?.GetValue<long>();

            if (live_status == 1 || live_status == 2 && IsToast == true)
            {
                // 业务层负责协调，Notifier 只接收 Uri
                var avatarUri = await ToastImageCache.GetLocalUriAsync("https://i1.hdslb.com/bfs/face/4c6e413e3789ef1bfaa738b05db977f4d7129858.jpg");
                var coverUri = await ToastImageCache.GetLocalUriAsync("https://i0.hdslb.com/bfs/live/new_room_cover/d57ca8a633b288333c1f0bb260a55ea46dca882b.jpg");

                // 2. 使用真实房间号拼接 URL 并发送通知
                string liveUrl = $"https://live.bilibili.com/{roomId}";

                await ToastNotifier.SendLiveNotificationAsync("headerTitle", "title", "subtitle", coverUri, avatarUri, liveUrl);
                IsToast = true;
            }
            if (live_status == 0)
            {
                IsToast = false;
            }
            await Task.Delay(Interval);
        }
    }
}