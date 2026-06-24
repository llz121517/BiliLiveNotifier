using BiliLiveNotifier.Core;

namespace BiliLiveNotifier;

class Program
{
    // 测试阶段的临时默认值，仅存在于入口处
    private const string TestHeaderId = "bili-live";
    private const string TestHeaderTitle = "BiliLiveNotifier";

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

        // 1. 先加载配置并查询真实房间号
        long uid = 496751305;
        ApiClient.LoadEndpoints();

        var data = await ApiClient.RequestAsync("GetMasterInfo", uid);
        long? roomId = data?.GetPath("room_id")?.GetValue<long>();

        if (!roomId.HasValue)
        {
            LLog.Warn($"[Init] UID:{uid} 解析直播间ID失败, 跳过通知发送");
            return;
        }

        LLog.Info($"[Init] UID:{uid} 解析到直播间ID: {roomId}");

        // 2. 使用真实房间号拼接 URL 并发送中文通知
        string liveUrl = $"https://live.bilibili.com/{roomId}";

        ToastNotifier.SendLiveNotification(
            headerId: TestHeaderId,
            headerTitle: TestHeaderTitle,
            title: "测试主播已开播!",
            subtitle: "点击按钮前往直播间",
            url: liveUrl
        );

        await Task.Delay(3000);
        // 业务层负责协调，ToastDemo 只接收 Uri
        var avatarUri = await ToastImageCache.GetLocalUriAsync("https://i1.hdslb.com/bfs/face/4c6e413e3789ef1bfaa738b05db977f4d7129858.jpg");
        var coverUri = await ToastImageCache.GetLocalUriAsync("https://i0.hdslb.com/bfs/live/new_room_cover/d57ca8a633b288333c1f0bb260a55ea46dca882b.jpg");

        // 传入已解析的本地 URI，ToastDemo 不感知网络/缓存
        ToastDemo.ShowMinimal();
        await Task.Delay(2000);
        ToastDemo.ShowWithAvatarAndTag(avatarUri);
        await Task.Delay(2000);
        ToastDemo.ShowWithCoverAndAvatar(coverUri, avatarUri);
        await Task.Delay(2000);
        ToastDemo.ShowWithDualButtons(avatarUri);

        ToastImageCache.ClearCache();

        await ApiClientTester.RunTestAsync();
    }
}