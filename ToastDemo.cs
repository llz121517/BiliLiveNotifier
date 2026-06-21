using Microsoft.Toolkit.Uwp.Notifications;
using System;
using Windows.Data.Xml.Dom;

namespace BiliLiveNotifier.Core;

/// <summary>
/// Toast 通知样式演示集
/// 所有图片参数均为已解析的本地 URI，模块内部无网络/缓存依赖
/// </summary>
public static class ToastDemo
{
    private const string DemoHeaderId = "bili-live-demo";
    private const string DemoHeaderTitle = "Style Demo";

    /// <summary>
    /// 内部辅助方法：安全构建 Toast 并输出调试 XML
    /// </summary>
    private static void ShowWithDebug(ToastContentBuilder builder, string styleName)
    {
        try
        {
            var toastContent = builder.GetToastContent();
            string xmlString = toastContent.GetXml().GetXml();

            LLog.Debug($"[ToastDemo] ===== {styleName} XML START =====");
            LLog.Debug(xmlString);
            LLog.Debug($"[ToastDemo] ===== {styleName} XML END =====");

            if (xmlString.Contains("src=\"\"") || xmlString.Contains("src=''"))
            {
                LLog.Warn($"[ToastDemo] 检测到空图片节点: {styleName}");
            }
            else if (xmlString.Contains("<image "))
            {
                LLog.Debug($"[ToastDemo] 图片节点校验通过: {styleName}");
            }

            builder.Show();
            LLog.Info($"[ToastDemo] 发送成功: {styleName}");
        }
        catch (Exception ex)
        {
            LLog.Error($"[ToastDemo] 发送失败: 样式={styleName}, 原因={ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 样式1：极简纯文本
    /// </summary>
    public static void ShowMinimal()
    {
        LLog.Debug("[ToastDemo] 参数: 无图片URI");

        var builder = new ToastContentBuilder()
            .AddHeader(DemoHeaderId, DemoHeaderTitle, "")
            .AddText("[Test Streamer] Live Now!")
            .AddText("Click button to enter room")
            .AddButton(new ToastButton()
                .SetContent("Enter Room")
                .AddArgument("action", "open")
                .AddArgument("url", "https://live.bilibili.com/12345")
                .SetBackgroundActivation());

        ShowWithDebug(builder, "Minimal");
    }

    /// <summary>
    /// 样式2：头像 + 分区标签
    /// </summary>
    public static void ShowWithAvatarAndTag(Uri avatarUri)
    {
        LLog.Debug($"[ToastDemo] 参数: AvatarUri={avatarUri?.ToString() ?? "null"}");

        if (avatarUri == null)
        {
            LLog.Warn("[ToastDemo] 头像URI为空, 降级为Minimal样式");
            ShowMinimal();
            return;
        }

        var builder = new ToastContentBuilder()
            .AddHeader(DemoHeaderId, DemoHeaderTitle, "")
            .AddAppLogoOverride(avatarUri, ToastGenericAppLogoCrop.Circle)
            .AddText("[Test Streamer] Live Now!")
            .AddText("Playing Apex Legends, pushing Master!")
            .AddAttributionText("VTuber · Daily")
            .AddButton(new ToastButton()
                .SetContent("Enter Room")
                .AddArgument("action", "open")
                .AddArgument("url", "https://live.bilibili.com/12345")
                .SetBackgroundActivation());

        ShowWithDebug(builder, "AvatarAndTag");
    }

    /// <summary>
    /// 样式3：封面大图 + 头像
    /// </summary>
    public static void ShowWithCoverAndAvatar(Uri coverUri, Uri avatarUri)
    {
        LLog.Debug($"[ToastDemo] 参数: CoverUri={coverUri?.ToString() ?? "null"}, AvatarUri={avatarUri?.ToString() ?? "null"}");

        if (coverUri == null && avatarUri == null)
        {
            LLog.Warn("[ToastDemo] 封面与头像URI均为空, 降级为Minimal样式");
            ShowMinimal();
            return;
        }

        var builder = new ToastContentBuilder()
            .AddHeader(DemoHeaderId, DemoHeaderTitle, "");

        if (coverUri != null)
            builder.AddHeroImage(coverUri);
        if (avatarUri != null)
            builder.AddAppLogoOverride(avatarUri, ToastGenericAppLogoCrop.Circle);

        builder.AddText("[Test Streamer] Live Now!")
            .AddText("Anniversary special event, limited merch giveaway!")
            .AddAttributionText("Entertainment · Daily")
            .AddButton(new ToastButton()
                .SetContent("Enter Room")
                .AddArgument("action", "open")
                .AddArgument("url", "https://live.bilibili.com/12345")
                .SetBackgroundActivation());

        ShowWithDebug(builder, "CoverAndAvatar");
    }

    /// <summary>
    /// 样式4：双按钮交互
    /// </summary>
    public static void ShowWithDualButtons(Uri avatarUri)
    {
        LLog.Debug($"[ToastDemo] 参数: AvatarUri={avatarUri?.ToString() ?? "null"}");

        if (avatarUri == null)
        {
            LLog.Warn("[ToastDemo] 头像URI为空, 降级为Minimal样式");
            ShowMinimal();
            return;
        }

        var builder = new ToastContentBuilder()
            .AddHeader(DemoHeaderId, DemoHeaderTitle, "")
            .AddAppLogoOverride(avatarUri, ToastGenericAppLogoCrop.Circle)
            .AddText("[Test Streamer] Live Now!")
            .AddText("Come chat together~")
            .AddAttributionText("VTuber · Daily")
            .AddButton(new ToastButton()
                .SetContent("Enter Room")
                .AddArgument("action", "open")
                .AddArgument("url", "https://live.bilibili.com/12345")
                .SetBackgroundActivation())
            .AddButton(new ToastButton()
                .SetContent("Quick Follow")
                .AddArgument("action", "follow")
                .AddArgument("uid", "496751305")
                .SetBackgroundActivation());

        ShowWithDebug(builder, "DualButtons");
    }
}