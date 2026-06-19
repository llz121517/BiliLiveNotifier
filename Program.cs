using Microsoft.Toolkit.Uwp.Notifications;

namespace BiliLiveNotifier;

class Program
{
    static void Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;

        LLog.Info("BiliLiveNotifier started");
        Core.InitToastListener();

        if (args.Length > 0)
        {
            LLog.Info("Launched by toast background activation, exiting gracefully.");
            return;
        }

        // 正常前台启动流程
        Core.SendTestNotification();
        LLog.Info("Waiting for user interaction... Press any key to exit.");
        Console.ReadKey();
    }
}