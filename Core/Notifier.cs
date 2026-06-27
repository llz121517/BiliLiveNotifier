using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BiliLiveNotifier.Core;

/// <summary>
/// Windows 系统 Toast 通知管理器
/// 仅负责通知的构建与发送，不处理任何资源获取逻辑
/// </summary>
public static class ToastNotifier
{
    /// <summary>
    /// 固定分组标识，与显示文案解耦
    /// </summary>
    private const string HeaderId = "BiliLiveNotifier";

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
                    LLog.Error($"[Toast] 打开链接失败: 原因={ex.Message}");
                }
            }
        };

        LLog.Debug("[Toast] 监听器初始化完成");
    }

    /// <summary>
    /// 发送开播/特殊提醒通知 (CoverAndAvatar 样式)
    /// </summary>
    /// <param name="headerTitle">分组显示标题</param>
    /// <param name="title">主标题</param>
    /// <param name="subtitle">副标题/描述</param>
    /// <param name="coverUrl">封面图本地URI (由调用方通过缓存服务获取)</param>
    /// <param name="avatarUrl">头像本地URI (由调用方通过缓存服务获取)</param>
    /// <param name="liveUrl">直播间链接</param>
    /// <param name="attribution">归属文本</param>
    public static async Task SendLiveNotificationAsync(
        string headerTitle,
        string title,
        string subtitle,
        Uri? coverUrl = null,
        Uri? avatarUrl = null,
        string? liveUrl = null,
        string? attribution = null)
    {
        try
        {
            LLog.Debug($"[Toast] 图片解析完成: Cover={coverUrl}, Avatar={avatarUrl}");

            var builder = new ToastContentBuilder()
                .AddHeader(HeaderId, headerTitle, "");

            if (coverUrl != null)
                builder.AddHeroImage(coverUrl);
            if (avatarUrl != null)
                builder.AddAppLogoOverride(avatarUrl, ToastGenericAppLogoCrop.Circle);

            builder.AddText(title)
                .AddText(subtitle);

            if (!string.IsNullOrEmpty(attribution))
                builder.AddAttributionText(attribution);

            if (!string.IsNullOrEmpty(liveUrl))
            {
                builder.AddButton(new ToastButton()
                    .SetContent("进入直播间")
                    .AddArgument("action", "open")
                    .AddArgument("url", liveUrl)
                    .SetBackgroundActivation());
            }

            builder.Show();
            LLog.Info($"[Toast] 通知已发送: 标题={title}");
        }
        catch (Exception ex)
        {
            LLog.Error($"[Toast] 通知发送失败: 原因={ex.Message}");
        }
    }
}