// Program.cs 
using BiliLiveNotifier.Core;

namespace BiliLiveNotifier;

class Program
{
    // 测试阶段的临时默认值，仅存在于入口处
    private const string TestHeaderId = "bili-live";
    private const string TestHeaderTitle = "BiliLiveNotifier";
    private const string TestUrl = "https://live.bilibili.com/35342534";

    static async Task Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;
        LLog.Info("----- Program Run");
        LLog.Info("BiliLiveNotifier started");
        ToastNotifier.InitListener();

        if (args.Length > 0)
        {
            LLog.Info("Launched by toast background activation, sleeping...");
            Thread.Sleep(5000);
            return;
        }

        // 所有配置由调用方传入
        ToastNotifier.SendLiveNotification(
            headerId: TestHeaderId,
            headerTitle: TestHeaderTitle,
            title: "Test streamer is live!",
            subtitle: "Click button to open live room",
            url: TestUrl
        );

        long uid = 546195;

        ApiClient.LoadEndpoints();

        // 获取房间号
        var data = await ApiClient.RequestAsync("GetRoomIdByUid", uid);
        long? roomId = data.GetPath("info", "room_id")?.GetValue<long>();

        if (roomId.HasValue)
            LLog.Info($"[Init] UID:{uid} -> RoomID:{roomId}");
        else
            LLog.Warn($"[Init] UID:{uid} Failed to resolve RoomID");
    }
}