using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;

namespace BiliLiveNotifier.Core;

/// <summary>
/// Windows 系统 Toast 通知管理器。
/// 负责通知的构建、发送以及点击事件的监听。
/// </summary>
public static class ToastNotifier
{
    /// <summary>
    /// 初始化 Toast 激活监听器 (通常在程序启动时调用一次)
    /// </summary>
    public static void InitListener()
    {
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            LLog.Info($"[Toast] 收到激活事件, 参数: {toastArgs.Argument}");

            var arguments = ToastArguments.Parse(toastArgs.Argument);
            if (arguments.TryGetValue("url", out var targetUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
                    LLog.Info($"[Toast] 已成功打开链接: {targetUrl}");
                }
                catch (Exception ex)
                {
                    LLog.Error($"[Toast] 打开链接失败: {ex.Message}");
                }
            }
        };

        LLog.Debug("[Toast] 监听器初始化完成");
    }

    /// <summary>
    /// 发送开播/特殊提醒通知
    /// </summary>
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
            LLog.Info($"[Toast] 通知已发送, 标题: {title}");
        }
        catch (Exception ex)
        {
            LLog.Error($"[Toast] 通知发送失败: {ex.Message}");
        }
    }
}