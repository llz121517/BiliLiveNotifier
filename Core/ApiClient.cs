// Core/ApiClient.cs
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace BiliLiveNotifier.Core;

public static class BiliApiClient
{
    private static readonly HttpClient _http;

    static BiliApiClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://space.bilibili.com/");
    }

    /// <summary>
    /// 统一 GET 请求入口
    /// </summary>
    public static async Task<ApiModels<T>?> GetAsync<T>(
        string url,
        int maxRetries = 20,
        CancellationToken ct = default)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var json = await _http.GetStringAsync(url, ct);

                var response = JsonSerializer.Deserialize<ApiModels<T>>(json);

                if (response == null) continue;
                if (response.Code == 0) return response;
                if (response.Code == -799) continue;

                LLog.Warn($"[API 业务错误] 地址: {url}, 错误码: {response.Code}, 信息: {response.Message}");
                return response;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LLog.Debug($"[API 请求异常] 第 {attempt + 1}/{maxRetries + 1} 次尝试失败: {ex.Message}");
                continue;
            }
        }

        LLog.Error($"[API 彻底失败] 已耗尽所有重试次数, 地址: {url}");
        return null;
    }
}