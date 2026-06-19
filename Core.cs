using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BiliLiveNotifier;

static class Core
{
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
    /// <param name="headerId">通知分组头ID</param>
    /// <param name="headerTitle">通知分组头显示名称</param>
    /// <param name="title">通知标题</param>
    /// <param name="subtitle">通知副标题</param>
    /// <param name="url">点击跳转链接（可选）</param>
    public static void SendLiveNotification(
        string headerId,
        string headerTitle,
        string title,
        string subtitle,
        string? url = null)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddHeader(headerId, headerTitle, "")
                .AddText(title)
                .AddText(subtitle);

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
}