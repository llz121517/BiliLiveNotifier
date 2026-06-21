using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BiliLiveNotifier.Core;

public static class ApiClient
{
    private static readonly HttpClient _http;
    private static Dictionary<string, ApiEndpointConfig>? _endpoints;

    static ApiClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Referer", "https://space.bilibili.com/");
    }

    /// <summary>
    /// 启动时调用一次，加载 API 端点配置
    /// </summary>
    public static void LoadEndpoints(string jsonPath = "api_endpoints.json")
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"API 端点配置文件未找到: {jsonPath}");

        var json = File.ReadAllText(jsonPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _endpoints = JsonSerializer.Deserialize<Dictionary<string, ApiEndpointConfig>>(json)
            ?? throw new InvalidOperationException("API 端点配置文件内容为空");

        LLog.Info($"[Api] 已加载 {_endpoints.Count} 个端点配置");
    }

    /// <summary>
    /// 配置驱动的通用 GET 请求，返回原始 JsonObject
    /// </summary>
    public static async Task<JsonObject?> RequestAsync(
        string endpointName,
        params object[] args)
    {
        if (_endpoints == null)
            throw new InvalidOperationException("请先调用 LoadEndpoints() 加载配置");

        if (!_endpoints.TryGetValue(endpointName, out var config))
            throw new ArgumentException($"未知的 API 端点: '{endpointName}'");

        // 构建查询参数（支持 {0},{1}... 占位符）
        var queryParams = new List<string>();
        int argIndex = 0;
        foreach (var (key, template) in config.Params)
        {
            string value = template;
            while (value.Contains($"{{{argIndex}}}"))
            {
                value = value.Replace($"{{{argIndex}}}", args[argIndex]?.ToString() ?? "");
                argIndex++;
            }
            queryParams.Add($"{key}={Uri.EscapeDataString(value)}");
        }

        string url = $"{config.Url}?{string.Join("&", queryParams)}";
        LLog.Debug($"[Api] 发起请求 [{endpointName}] {url}");

        for (int attempt = 0; attempt <= 20; attempt++)
        {
            try
            {
                var rawJson = await _http.GetStringAsync(url);

                LLog.Debug($"[Api] [{endpointName}] 原始响应: {rawJson}");

                var response = JsonSerializer.Deserialize<ApiModels<JsonObject>>(rawJson);
                if (response == null) continue;
                if (response.Code == 0 && response.Data != null) return response.Data;
                if (response.Code == -799)
                {
                    LLog.Debug($"[Api] [{endpointName}] 触发风控(-799), 准备重试...");
                    continue;
                }

                LLog.Warn($"[Api] [{endpointName}] 业务错误, 错误码: {response.Code}, 信息: {response.Message}");
                return null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // 仅在非首次失败或最后一次失败时记录，避免刷屏
                if (attempt > 0 || attempt == 20)
                    LLog.Debug($"[Api] [{endpointName}] 第 {attempt + 1}/21 次请求异常: {ex.Message}");
                continue;
            }
        }

        LLog.Error($"[Api] [{endpointName}] 重试次数已耗尽");
        return null;
    }
}