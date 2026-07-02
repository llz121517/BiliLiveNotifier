using BiliLiveNotifier.Core;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace BiliLiveNotifier;

class Program
{
    public static int Interval { get; set; }
    public static int LiveInterval { get; set; }
    public static bool BirthdayText { get; set; }
    public static bool FilterBirthday { get; set; }
    public static bool IsToast { get; set; } = false;
    // 记录上一次检查生日的日期字符串 (格式: MM-dd)
    private static string _lastBirthdayCheckDate = string.Empty;
    // 生日标记
    private static bool _isBirthday = false;
    static async Task Main(string[] args)
    {
        LLog.Level = LLog.LogLevel.Debug;
        LLog.Line();
        LLog.Info("===== BiliLiveNotifier v0.1.0-Dev Started =====");

        ToastNotifier.InitListener();

        if (args.Length > 0)
        {
            LLog.Info("[Main] 由 Toast 后台激活启动, 进入休眠等待...");
            await Task.Delay(5000);
            return;
        }

        // 1. Init
        ConfigManager.Initialize();
        ApiClient.LoadEndpoints();

        var cfg = ConfigManager.Config;
        long[] uids = cfg?["uids"]?.AsArray()
            .Select(v => v is not null ? (long)v : 0L)
            .ToArray() ?? [];
        Interval = (cfg?["check_interval"]?.GetValue<int>() ?? 15) * 1000;
        LiveInterval = (cfg?["live_check_interval"]?.GetValue<int>() ?? 45) * 1000;
        BirthdayText = cfg?["birthday_text"]?.GetValue<bool>() ?? true;
        FilterBirthday = cfg?["skip_default_birthday"]?.GetValue<bool>() ?? true;
        bool autoStart = cfg?["auto_start"]?.GetValue<bool>() ?? false;

        LLog.Info("[Main] 初始化完成\n");

        await Program.MonitorService(uids[0]);

        ToastImageCache.ClearCache();
    }

    /// <summary>
    /// 每日检查一次生日，仅在日期变更时请求API
    /// </summary>
    private static async Task CheckBirthdayAsync(long uid)
    {
        string today = DateTime.Now.ToString("MM-dd");

        // 如果今天已经检查过，直接跳过
        if (_lastBirthdayCheckDate == today) return;

        LLog.Debug($"[CheckBirthday] 检测到新的一天 ({today})，开始检查生日...");

        try
        {
            var userInfo = await ApiClient.RequestMappedAsync("GetUserInfo", uid);
            string? birthday = userInfo?.GetValueOrDefault("birthday")?.ToString();

            // 如果配置了过滤默认生日，并且生日是 "01-01"，则直接跳过
            if (FilterBirthday && birthday == "01-01")
            {
                LLog.Info($"[CheckBirthday] UID={uid} 生日={birthday}, 过滤配置={FilterBirthday}, 跳过");
                _lastBirthdayCheckDate = today;
                return;
            }

            bool isMatch = !string.IsNullOrEmpty(birthday) &&
                           (birthday.Equals(today, StringComparison.OrdinalIgnoreCase) ||
                            birthday.EndsWith(today, StringComparison.OrdinalIgnoreCase));

            _isBirthday = isMatch;
            _lastBirthdayCheckDate = today;

            LLog.Info($"[CheckBirthday] UID={uid} 生日={birthday}, 今日={today}, 匹配={_isBirthday}");
        }
        catch (Exception ex)
        {
            LLog.Error($"[CheckBirthday] 检查失败: {ex.Message}");
        }
    }

    static async Task MonitorService(long Uid)
    {
        var data = await ApiClient.RequestMappedAsync("GetMasterInfo", Uid);
        long? roomId = data?["roomId"] is not null ? (long?)data["roomId"] : null;

        if (!roomId.HasValue)
        {
            LLog.Warn($"[MonitorService] UID={Uid} 解析直播间ID失败, 跳过通知发送");
            return;
        }

        LLog.Info($"[MonitorService] UID={Uid} 解析到直播间ID: {roomId}");

        ConfigManager.OnConfigReloaded += newConfig =>
        {
            LLog.Info("[Reload] 检测到配置变更，准备热重载...");

            // 获取当前可执行文件的真实路径
            string? exePath = Environment.ProcessPath;

            LLog.Debug($"[Reload] 正在 ReBoot : {exePath}");

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                });

                // 优雅退出当前进程
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                LLog.Error($"[Reload] 启动新进程失败: {ex.Message}");
            }
        };

        while (true)
        {
            string? medalName = null;
            string? title = null;
            string? time = null;
            long? watchedNum = null;

            await CheckBirthdayAsync(Uid);

            data = await ApiClient.RequestMappedAsync("GetLiveRoomDetail", roomId);
            long? live_status = data?["liveStatus"] is not null ? (long?)data["liveStatus"] : null;

            if ((live_status == 1 || live_status == 2) && !IsToast)
            {
                // -- GetLiveRoomDetail → online, parentAreaName, areaName, liveTime --
                long? online = data?.GetValueOrDefault("online") as long?;
                string? parentAreaName = data?.GetValueOrDefault("parentAreaName")?.ToString();
                string? areaName = data?.GetValueOrDefault("areaName")?.ToString();
                string? liveTime = data?.GetValueOrDefault("liveTime")?.ToString();

                // -- GetMasterInfo → medalName --
                var masterInfo = await ApiClient.RequestMappedAsync("GetMasterInfo", Uid);
                medalName = masterInfo?.GetValueOrDefault("medalName")?.ToString();

                // -- GetUserInfo → name, face, title, cover, watchedNum --
                var userInfo = await ApiClient.RequestMappedAsync("GetUserInfo", Uid);
                string? name = userInfo?.GetValueOrDefault("name")?.ToString();
                string? face = userInfo?.GetValueOrDefault("face")?.ToString();
                title = userInfo?.GetValueOrDefault("title")?.ToString();
                string? cover = userInfo?.GetValueOrDefault("cover")?.ToString();
                watchedNum = userInfo?.GetValueOrDefault("watchedNum") as long?;

                // ========================================
                // 已收集以下数据供通知使用：
                //   房间：title, cover, parentAreaName, areaName, online, liveTime
                //   主播：name, face
                //   勋章：medalName
                //   观看：watchedNum, online
                //   生日标记：_isBirthday
                // ========================================

                Uri? avatarUri = null;
                Uri? coverUri = null;

                if (face != null && cover != null)
                {
                    avatarUri = await ToastImageCache.GetLocalUriAsync(face);
                    coverUri = await ToastImageCache.GetLocalUriAsync(cover);
                }

                string ToasttTitle;

                if (_isBirthday && BirthdayText)
                {
                    ToasttTitle = "亲爱的「" + medalName + "」！今天是个特别的日子哦";
                }
                else
                {
                    ToasttTitle = "亲爱的「" + medalName + "」！你关注的「" + name + "」开播啦！";
                }

                if (liveTime != null)
                {
                    time = DateTime.Parse(liveTime).ToString("HH:mm");
                }
                else
                {
                    time = "--:--";
                }

                await ToastNotifier.SendLiveNotificationAsync(
                    headerTitle: "BiliLiveNotifier",
                    title: ToasttTitle,
                    subtitle: $"标题：{title}\n开播时间：{time}\n{watchedNum} 人看过",
                    coverUrl: coverUri,
                    avatarUrl: avatarUri,
                    liveUrl: "https://live.bilibili.com/" + roomId,
                    attribution: parentAreaName + " • " + areaName
                );
                IsToast = true;
            }
            if (live_status == 0)
            {
                if (IsToast == true)
                {
                    await ToastNotifier.SendLiveNotificationAsync(
                        headerTitle: "BiliLiveNotifier",
                        title: "主播「" + medalName + "」下播啦",
                        subtitle: $"标题：{title}\n开播时间：{time}\n{watchedNum} 人看过"
                    );
                }
                IsToast = false;
            }
            if (IsToast)
            {
                await Task.Delay(LiveInterval);
            }
            else
            {
                await Task.Delay(Interval);
            }
        }
    }
}