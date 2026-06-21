// Core/Notifier.cs
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
            LLog.Info($"[Toast 激活] 参数: {toastArgs.Argument}");

            var arguments = ToastArguments.Parse(toastArgs.Argument);
            if (arguments.TryGetValue("url", out var targetUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
                    LLog.Info($"[浏览器已打开] 链接: {targetUrl}");
                }
                catch (Exception ex)
                {
                    LLog.Error($"[打开链接失败] 异常: {ex}");
                }
            }
        };

        LLog.Debug("[Toast 监听器] 初始化完成");
    }

    /// <summary>
    /// 发送开播/特殊提醒通知
    /// </summary>
    /// <param name="headerId">通知分组头ID (用于将同一主播的通知折叠)</param>
    /// <param name="headerTitle">通知分组头显示名称 (如：主播昵称)</param>
    /// <param name="title">通知标题 (如：直播间标题)</param>
    /// <param name="subtitle">通知副标题 (如：分区信息、开播时长)</param>
    /// <param name="url">点击跳转链接（可选，通常为直播间 URL）</param>
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
            LLog.Info($"[Toast 已发送] 标题: {title}");
        }
        catch (Exception ex)
        {
            LLog.Error($"[Toast 发送失败] 异常: {ex}");
        }
    }
}