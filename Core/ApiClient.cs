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
            throw new FileNotFoundException($"API endpoints config not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);
        _endpoints = JsonSerializer.Deserialize<Dictionary<string, ApiEndpointConfig>>(json)
            ?? throw new InvalidOperationException("API endpoints config is empty");

        LLog.Info($"[ApiClient] Loaded {_endpoints.Count} endpoint(s) from {jsonPath}");
    }

    /// <summary>
    /// 配置驱动的通用 GET 请求，返回原始 JsonObject
    /// </summary>
    public static async Task<JsonObject?> RequestAsync(
        string endpointName,
        params object[] args)
    {
        if (_endpoints == null)
            throw new InvalidOperationException("Call LoadEndpoints() before requesting");

        if (!_endpoints.TryGetValue(endpointName, out var config))
            throw new ArgumentException($"Unknown API endpoint: '{endpointName}'");

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
        LLog.Debug($"[ApiClient] [{endpointName}] {config.Method} {url}");

        for (int attempt = 0; attempt <= 20; attempt++)
        {
            try
            {
                var rawJson = await _http.GetStringAsync(url);
                LLog.Debug($"[ApiClient] [{endpointName}] Raw Response: {rawJson}");

                var response = JsonSerializer.Deserialize<ApiModels<JsonObject>>(rawJson);
                if (response == null) continue;
                if (response.Code == 0 && response.Data != null) return response.Data;
                if (response.Code == -799) continue;

                LLog.Warn($"[ApiClient] [{endpointName}] Biz Error Code:{response.Code}, Msg:{response.Message}");
                return null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LLog.Debug($"[ApiClient] [{endpointName}] Attempt {attempt + 1}/21 failed: {ex.Message}");
                continue;
            }
        }

        LLog.Error($"[ApiClient] [{endpointName}] All retries exhausted");
        return null;
    }
}