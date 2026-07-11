using BiliLiveNotifier.Core;
using System.Text.Json.Nodes;

namespace BiliLiveNotifier;

/// <summary>
/// 全局应用上下文，持有共享依赖并管理所有监控实例的生命周期
/// </summary>
public class LiveEnvironment
{
    public NotifierConfig Config { get; private set; }

    // uid → (监控实例, 运行任务)
    private readonly Dictionary<long, (LiveMonitor Monitor, Task Task)> _monitors = new();

    public LiveEnvironment(NotifierConfig config)
    {
        Config = config;
    }

    /// <summary>
    /// 启动所有配置中的 UID 监控
    /// </summary>
    public async Task StartAllAsync()
    {
        foreach (var uid in Config.Uids)
        {
            StartMonitor(uid);
            await Task.Delay(1000); // 避免同时请求过快
        }
        LLog.Info($"[Env] 已启动 {_monitors.Count} 个监控实例");
    }

    /// <summary>
    /// 优雅关闭所有监控
    /// </summary>
    public async Task StopAllAsync()
    {
        foreach (var (monitor, _) in _monitors.Values)
            monitor.Stop();

        await Task.WhenAll(_monitors.Values.Select(v => v.Task));
        _monitors.Clear();
        LLog.Info("[Env] 所有监控实例已停止");
    }

    /// <summary>
    /// 配置热重载：根据 UID 差量增删监控实例，其它配置自动生效
    /// </summary>
    public async Task ReloadAsync()
    {
        var newConfig = NotifierConfig.FromJsonNode(ConfigManager.Config);
        var oldUids = _monitors.Keys.ToHashSet();
        var newUids = newConfig.Uids.ToHashSet();

        // ① 要移除的：在旧不在新 → Stop
        var toRemove = oldUids.Except(newUids).ToList();
        foreach (var uid in toRemove)
        {
            LLog.Info($"[Reload] 停止监控: {uid}");
            _monitors[uid].Monitor.Stop();
            await _monitors[uid].Task;
            _monitors.Remove(uid);
        }

        // ② 要新增的：在新不在旧 → New + Start
        var toAdd = newUids.Except(oldUids).ToList();
        foreach (var uid in toAdd)
        {
            LLog.Info($"[Reload] 新增监控: {uid}");
            StartMonitor(uid);
        }

        // ③ 更新配置引用（其它配置项如 CheckInterval 会被 LiveMonitor 自动读取）
        Config = newConfig;

        LLog.Info($"[Reload] 完成: 移除 {toRemove.Count}, 新增 {toAdd.Count}, 当前 {_monitors.Count} 个监控");
    }

    // ---- 私有方法 ----

    private void StartMonitor(long uid)
    {
        var monitor = new LiveMonitor(uid, this);
        var task = Task.Run(() => monitor.StartAsync());
        _monitors[uid] = (monitor, task);
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
        _roomId = masterData?.GetValueOrDefault("roomId") as long?;

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
            // 正常停止，无需处理
        }

        LLog.Info($"[Monitor:{_uid}] 已停止");
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
        // 旧模式：每日轮询时独立检查生日
        if (!_ctx.Config.BirthdayCheckOnLiveOnly)
            await CheckBirthdayAsync();

        var data = await ApiClient.RequestMappedAsync("GetLiveRoomDetail", _roomId);
        long? liveStatus = data?.GetValueOrDefault("liveStatus") as long?;

        if ((liveStatus == 1 || (liveStatus == 2 && _ctx.Config.NotifyOnCarousel)) && !_isToast)
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
        string? cover = roomData?.GetValueOrDefault("cover")?.ToString();

        // -- 主播信息 --
        var userInfo = await ApiClient.RequestMappedAsync("GetUserInfo", _uid);
        var masterInfo = await ApiClient.RequestMappedAsync("GetMasterInfo", _uid);

        string? name = masterInfo?.GetValueOrDefault("uname")?.ToString();
        string? face = masterInfo?.GetValueOrDefault("face")?.ToString();
        string? birthday = userInfo?.GetValueOrDefault("birthday")?.ToString();

        // 新模式：开播时从 GetUserInfo 顺带查生日
        if (_ctx.Config.BirthdayCheckOnLiveOnly)
        {
            string today = DateTime.Now.ToString("MM-dd");
            _isBirthday = false;
            if (!string.IsNullOrEmpty(birthday))
            {
                if (_ctx.Config.FilterBirthday && birthday == "01-01")
                {
                    LLog.Info($"[Monitor:{_uid}] 生日={birthday}, 已过滤");
                }
                else if (birthday.Equals(today, StringComparison.OrdinalIgnoreCase) ||
                         birthday.EndsWith(today, StringComparison.OrdinalIgnoreCase))
                {
                    _isBirthday = true;
                    LLog.Info($"[Monitor:{_uid}] 生日={birthday}, 今日={today}, 匹配=true");
                }
            }
        }

        // 持久化到实例字段，供后续循环使用
        _medalName = masterInfo?.GetValueOrDefault("medalName")?.ToString();
        _title = roomData?.GetValueOrDefault("title")?.ToString();
        _watchedNum = userInfo?.GetValueOrDefault("watchedNum") as long?;
        _time = ParseLiveTime(liveTime);

        // -- 图片缓存 --
        Uri? avatarUri = null, coverUri = null;
        if (face != null && cover != null)
        {
            avatarUri = await ToastImageCache.GetLocalUriAsync(face);
            coverUri = await ToastImageCache.GetHeroUriAsync(cover);
        }

        // -- 构建标题 --
        string toastTitle = (_isBirthday && _ctx.Config.BirthdayText)
            ? $"亲爱的「{_medalName}」！今天是个特别的日子哦"
            : $"亲爱的「{_medalName}」！你关注的「{name}」开播啦！";

        string toastSub = (_watchedNum == null)
            ? $"标题：{_title}\n开播时间：{_time}"
            : $"标题：{_title}\n开播时间：{_time}\n{_watchedNum} 人看过";

        await ToastNotifier.SendLiveNotificationAsync(
            headerTitle: "BiliLiveNotifier",
            title: toastTitle,
            subtitle: toastSub,
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
    /// 旧模式：每日检查一次生日，仅在日期变更时请求 API
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