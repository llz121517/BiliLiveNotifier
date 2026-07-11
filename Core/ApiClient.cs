using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BiliLiveNotifier.Tests")]

namespace BiliLiveNotifier.Core;

/// <summary>
/// Bilibili API 通用客户端
/// </summary>
public static class ApiClient
{
    private static HttpClient _http = null!;
    private static Dictionary<string, ApiEndpointConfig>? _endpoints;

    internal static HttpClient TestHttpClient
    {
        set => _http = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal static void ResetHttpClient()
    {
        TestHttpClient = CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Referer", "https://space.bilibili.com/");
        return client;
    }

    static ApiClient()
    {
        _http = CreateDefaultHttpClient();
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

        _endpoints = JsonSerializer.Deserialize<Dictionary<string, ApiEndpointConfig>>(json, options)
            ?? throw new InvalidOperationException("API 端点配置文件内容为空");

        LLog.Info($"[Api] 已加载 {_endpoints.Count} 个端点配置");
        foreach (var kv in _endpoints)
        {
            LLog.Debug($"[Api] 端点 '{kv.Key}' -> 映射字段数: {kv.Value.ResponseMapping.Count}");
            // 如需查看具体映射，可取消下面注释
            // foreach (var map in kv.Value.ResponseMapping)
            //     LLog.Debug($"    {map.Key} -> {map.Value}");
        }
    }

    /// <summary>
    /// 通用 GET 请求，返回原始 JsonObject（已解包 data 字段）
    /// </summary>
    public static async Task<JsonObject?> RequestAsync(
        string endpointName,
        params object[] args)
    {
        if (_endpoints == null)
            throw new InvalidOperationException("请先调用 LoadEndpoints() 加载配置");

        if (!_endpoints.TryGetValue(endpointName, out var config))
            throw new ArgumentException($"未知的 API 端点: '{endpointName}'");

        // 构建查询参数
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
        LLog.Debug($"[Api] 请求 [{endpointName}] -> {url}");

        for (int attempt = 0; attempt <= 14; attempt++)
        {
            try
            {
                var rawJson = await _http.GetStringAsync(url);
                var response = JsonSerializer.Deserialize<ApiModels<JsonObject>>(rawJson);
                if (response == null)
                {
                    LLog.Warn($"[Api] [{endpointName}] 响应反序列化为 null");
                    continue;
                }

                if (response.Code == 0 && response.Data != null)
                {
                    LLog.Debug($"[Api] [{endpointName}] 请求成功 (code=0)");
                    return response.Data;
                }

                if (response.Code == -799)
                {
                    LLog.Debug($"[Api] [{endpointName}] 触发风控 -799，300ms 后重试...");
                    await Task.Delay(300);
                    continue;
                }

                LLog.Warn($"[Api] [{endpointName}] 业务错误: code={response.Code}, msg={response.Message}");
                return null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (attempt == 0)
                    LLog.Debug($"[Api] [{endpointName}] 首次请求异常: {ex.Message}");
                else if (attempt == 15)
                    LLog.Debug($"[Api] [{endpointName}] 重试耗尽: {ex.Message}");
                else
                    LLog.Debug($"[Api] [{endpointName}] 第 {attempt + 1} 次重试异常: {ex.Message}");
                // 继续重试
            }
        }

        LLog.Error($"[Api] [{endpointName}] 重试次数已耗尽，请求失败");
        return null;
    }

    /// <summary>
    /// 根据配置映射响应字段，直接返回字典
    /// </summary>
    public static async Task<Dictionary<string, object?>?> RequestMappedAsync(
        string endpointName,
        params object[] args)
    {
        var data = await RequestAsync(endpointName, args);
        if (data == null) return null;
        return MapResponse(endpointName, data);
    }

    /// <summary>
    /// 将 JsonObject 按端点配置的 responseMapping 提取为字典
    /// </summary>
    private static Dictionary<string, object?> MapResponse(string endpointName, JsonObject data)
    {
        if (_endpoints == null)
            throw new InvalidOperationException("请先调用 LoadEndpoints() 加载配置");

        if (!_endpoints.TryGetValue(endpointName, out var config))
            throw new ArgumentException($"未知的 API 端点: '{endpointName}'");

        LLog.Debug($"[Api] 开始映射响应数据 [{endpointName}] (共 {config.ResponseMapping.Count} 个字段)");

        var result = new Dictionary<string, object?>();
        foreach (var kv in config.ResponseMapping)
        {
            var node = GetPathNode(data, kv.Value);
            var value = ConvertJsonNodeToValue(node);
            if (value == null)
            {
                // 只在字段为 null 时记录调试信息，便于发现路径错误
                LLog.Debug($"[Api] 字段 '{kv.Key}' -> 路径 '{kv.Value}' 未找到或为 null");
            }
            result[kv.Key] = value;
        }
        return result;
    }

    private static JsonNode? GetPathNode(JsonNode? node, string path)
    {
        if (node == null) return null;
        var segments = path.Split('.');
        JsonNode? current = node;
        foreach (var seg in segments)
        {
            if (current is JsonObject obj && obj.TryGetPropertyValue(seg, out var child))
            {
                current = child;
            }
            else
            {
                return null;
            }
        }
        return current;
    }

    private static object? ConvertJsonNodeToValue(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue value)
        {
            var kind = value.GetValueKind();
            return kind switch
            {
                JsonValueKind.String => value.GetValue<string>(),
                JsonValueKind.Number => value.GetValue<long>(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => value,
                JsonValueKind.Array => value,
                _ => value.GetValue<object>()
            };
        }
        return node;
    }
}