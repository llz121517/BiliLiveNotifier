using BiliLiveNotifier.Core;
using System.Text.Json.Nodes;

namespace BiliLiveNotifier;

/// <summary>
/// 全局应用上下文，持有共享依赖
/// </summary>
public class LiveEnvironment
{
    public NotifierConfig Config { get; set; }

    public LiveEnvironment(NotifierConfig config)
    {
        Config = config;
    }

    /// <summary>
    /// 从 ConfigManager 重新加载配置
    /// </summary>
    public void ReloadConfig()
    {
        Config = NotifierConfig.FromJsonNode(ConfigManager.Config);
    }
}

/// <summary>
/// 单个主播的直播间监控实例
/// </summary>
public class LiveMonitor
{
    // ---- 依赖 ----
    private readonly long _uid;
    private readonly LiveEnvironment _ctx;

    // ---- 实例状态 ----
    private bool _isToast;
    private bool _isBirthday;
    private string _lastBirthdayCheckDate = string.Empty;

    // ---- 跨循环持久化的通知数据 ----
    private string? _medalName;
    private string? _title;
    private string? _time;
    private long? _watchedNum;
    private long? _roomId;

    // ---- 停止控制 ----
    private readonly CancellationTokenSource _cts = new();

    public long Uid => _uid;
    public bool IsRunning => !_cts.IsCancellationRequested;

    public LiveMonitor(long uid, LiveEnvironment ctx)
    {
        _uid = uid;
        _ctx = ctx;
    }

    /// <summary>
    /// 启动监控循环（阻塞直到 Stop 被调用）
    /// </summary>
    public async Task StartAsync()
    {
        var masterData = await ApiClient.RequestMappedAsync("GetMasterInfo", _uid);
        _roomId = masterData?["roomId"] is not null ? (long?)masterData["roomId"] : null;

        if (!_roomId.HasValue)
        {
            LLog.Warn($"[Monitor:{_uid}] 解析直播间ID失败, 跳过");
            return;
        }

        LLog.Info($"[Monitor:{_uid}] 直播间ID: {_roomId}");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await TickAsync();
                await Task.Delay(GetDelay(), _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            LLog.Info($"[Monitor:{_uid}] 已停止");
        }
    }

    /// <summary>
    /// 请求停止监控
    /// </summary>
    public void Stop()
    {
        _cts.Cancel();
    }

    // ==================== 私有方法 ====================

    /// <summary>
    /// 单次检查循环
    /// </summary>
    private async Task TickAsync()
    {
        await CheckBirthdayAsync();

        var data = await ApiClient.RequestMappedAsync("GetLiveRoomDetail", _roomId);
        long? liveStatus = data?["liveStatus"] is not null ? (long?)data["liveStatus"] : null;

        if ((liveStatus == 1 || liveStatus == 2) && !_isToast)
        {
            await HandleGoLiveAsync(data);
        }
        else if (liveStatus == 0 && _isToast)
        {
            await HandleGoOfflineAsync();
        }
    }

    /// <summary>
    /// 开播处理
    /// </summary>
    private async Task HandleGoLiveAsync(Dictionary<string, object?>? roomData)
    {
        // -- 房间信息 --
        string? parentAreaName = roomData?.GetValueOrDefault("parentAreaName")?.ToString();
        string? areaName = roomData?.GetValueOrDefault("areaName")?.ToString();
        string? liveTime = roomData?.GetValueOrDefault("liveTime")?.ToString();

        // -- 主播信息 --
        var userInfo = await ApiClient.RequestMappedAsync("GetUserInfo", _uid);
        var masterInfo = await ApiClient.RequestMappedAsync("GetMasterInfo", _uid);

        string? name = userInfo?.GetValueOrDefault("name")?.ToString();
        string? face = userInfo?.GetValueOrDefault("face")?.ToString();
        string? cover = userInfo?.GetValueOrDefault("cover")?.ToString();

        // 持久化到实例字段，供后续循环使用
        _medalName = masterInfo?.GetValueOrDefault("medalName")?.ToString();
        _title = userInfo?.GetValueOrDefault("title")?.ToString();
        _watchedNum = userInfo?.GetValueOrDefault("watchedNum") as long?;
        _time = ParseLiveTime(liveTime);

        // -- 图片缓存 --
        Uri? avatarUri = null, coverUri = null;
        if (face != null && cover != null)
        {
            avatarUri = await ToastImageCache.GetLocalUriAsync(face);
            coverUri = await ToastImageCache.GetLocalUriAsync(cover);
        }

        // -- 构建标题 --
        string toastTitle = (_isBirthday && _ctx.Config.BirthdayText)
            ? $"亲爱的「{_medalName}」！今天是个特别的日子哦"
            : $"亲爱的「{_medalName}」！你关注的「{name}」开播啦！";

        await ToastNotifier.SendLiveNotificationAsync(
            headerTitle: "BiliLiveNotifier",
            title: toastTitle,
            subtitle: $"标题：{_title}\n开播时间：{_time}\n{_watchedNum} 人看过",
            coverUrl: coverUri,
            avatarUrl: avatarUri,
            liveUrl: $"https://live.bilibili.com/{_roomId}",
            attribution: $"{parentAreaName} • {areaName}"
        );

        _isToast = true;
    }

    /// <summary>
    /// 下播处理
    /// </summary>
    private async Task HandleGoOfflineAsync()
    {
        await ToastNotifier.SendLiveNotificationAsync(
            headerTitle: "BiliLiveNotifier",
            title: $"主播「{_medalName}」下播啦",
            subtitle: $"标题：{_title}\n开播时间：{_time}\n{_watchedNum} 人看过"
        );
        _isToast = false;
    }

    /// <summary>
    /// 每日检查一次生日，仅在日期变更时请求API
    /// </summary>
    private async Task CheckBirthdayAsync()
    {
        string today = DateTime.Now.ToString("MM-dd");
        if (_lastBirthdayCheckDate == today) return;

        LLog.Debug($"[Monitor:{_uid}] 检测到新的一天 ({today})，检查生日...");

        try
        {
            var userInfo = await ApiClient.RequestMappedAsync("GetUserInfo", _uid);
            string? birthday = userInfo?.GetValueOrDefault("birthday")?.ToString();

            if (_ctx.Config.FilterBirthday && birthday == "01-01")
            {
                LLog.Info($"[Monitor:{_uid}] 生日={birthday}, 已过滤");
                _lastBirthdayCheckDate = today;
                return;
            }

            _isBirthday = !string.IsNullOrEmpty(birthday) &&
                          (birthday.Equals(today, StringComparison.OrdinalIgnoreCase) ||
                           birthday.EndsWith(today, StringComparison.OrdinalIgnoreCase));
            _lastBirthdayCheckDate = today;

            LLog.Info($"[Monitor:{_uid}] 生日={birthday}, 今日={today}, 匹配={_isBirthday}");
        }
        catch (Exception ex)
        {
            LLog.Error($"[Monitor:{_uid}] 生日检查失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据当前状态返回轮询间隔（毫秒）
    /// </summary>
    private int GetDelay()
    {
        return _isToast
            ? _ctx.Config.LiveCheckInterval * 1000
            : _ctx.Config.CheckInterval * 1000;
    }

    /// <summary>
    /// 解析开播时间戳为 HH:mm
    /// </summary>
    private static string ParseLiveTime(string? liveTime)
    {
        if (liveTime is null) return "--:--";
        return DateTime.TryParse(liveTime, out var dt) ? dt.ToString("HH:mm") : "--:--";
    }
}