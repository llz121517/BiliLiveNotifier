using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BiliLiveNotifier;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("🟢 BiliLiveNotifier Toast PoC (v3.0+ 兼容版) 启动...");

        const string testUrl = "https://live.bilibili.com/123456";

        // 1. 构建通知内容
        var toast = new ToastContentBuilder()
            .AddHeader("BiliLiveNotifier", "开播提醒测试", "")
            .AddText("🎉 测试主播 开播啦！")
            .AddText("这是一条来自 C# 重构版的原生 Toast 通知测试。")
            .AddArgument("url", testUrl) // 关键：将 URL 作为参数嵌入
            .Build();

        // 2. 创建 ToastNotification 实例，并绑定点击事件
        var notification = new ToastNotification(toast.GetXml());

        // ✅ 重点：使用实例的 Activated 事件（线程安全，调试器下也有效！）
        notification.Activated += (_, _) =>
        {
            Console.WriteLine($"✅ 点击通知触发！尝试打开: {testUrl}");

            try
            {
                Process.Start(new ProcessStartInfo(testUrl)
                {
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 打开浏览器失败: {ex.Message}");
            }
        };

        // 3. 显示通知
        ToastNotificationManager.CreateToastNotifier().Show(notification);

        Console.WriteLine($"📤 已发送测试通知 (目标URL: {testUrl})");
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}