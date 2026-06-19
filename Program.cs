namespace BiliLiveNotifier;

class Program
{
    // 测试阶段的临时默认值，仅存在于入口处
    private const string TestHeaderId = "bili-live";
    private const string TestHeaderTitle = "BiliLiveNotifier";
    private const string TestUrl = "https://live.bilibili.com/35342534";

    static void Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;

        LLog.Info("BiliLiveNotifier started");
        Core.InitToastListener();

        if (args.Length > 0)
        {
            LLog.Info("Launched by toast background activation, sleeping...");
            Thread.Sleep(5000);
            return;
        }

        // 所有配置由调用方传入
        Core.SendLiveNotification(
            headerId: TestHeaderId,
            headerTitle: TestHeaderTitle,
            title: "Test streamer is live!",
            subtitle: "Click button to open live room",
            url: TestUrl
        );

        LLog.Info("Waiting for user interaction... Press any key to exit.");
        Console.ReadKey();
    }
}