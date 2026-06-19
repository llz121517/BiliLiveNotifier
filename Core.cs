using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BiliLiveNotifier;

/// <summary>
/// 核心业务层：封装通知、API请求等所有可复用逻辑
/// </summary>
static class Core
{
    private const string DefaultUrl = "https://live.bilibili.com/35342534";

    /// <summary>
    /// 初始化 Toast 全局激活事件监听
    /// </summary>
    public static void InitToastListener()
    {
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            LLog.Info($"Toast activated, args: {toastArgs.Argument}");

            var arguments = ToastArguments.Parse(toastArgs.Argument);
            if (arguments.TryGetValue("url", out var targetUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
                    LLog.Info($"Browser opened: {targetUrl}");
                }
                catch (Exception ex)
                {
                    LLog.Error($"Failed to open url: {ex}");
                }
            }
        };

        LLog.Debug("Toast listener initialized");
    }

    /// <summary>
    /// 发送开播通知
    /// </summary>
    public static void SendLiveNotification(string title, string subtitle, string? url = null)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddHeader("bili-live", "BiliLiveNotifier", "")
                .AddText(title)
                .AddText(subtitle);

            // 如果提供了URL，则添加跳转按钮
            if (!string.IsNullOrEmpty(url))
            {
                builder.AddButton(new ToastButton()
                    .SetContent("Open Live Room")
                    .AddArgument("action", "open")
                    .AddArgument("url", url)
                    .SetBackgroundActivation());
            }

            builder.Show();
            LLog.Info($"Toast sent: {title}");
        }
        catch (Exception ex)
        {
            LLog.Error($"Failed to send toast: {ex}");
        }
    }

    /// <summary>
    /// 发送测试通知（用于调试）
    /// </summary>
    public static void SendTestNotification()
    {
        SendLiveNotification(
            "Test streamer is live!",
            "Click button to open live room",
            DefaultUrl
        );
    }
}