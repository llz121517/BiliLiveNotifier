using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BiliLiveNotifier.Core;


/// <summary>
/// Bilibili API 通用客户端
/// </summary>
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
        LLog.Debug($"[Api] 配置文件原始内容: {json}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        _endpoints = JsonSerializer.Deserialize<Dictionary<string, ApiEndpointConfig>>(json, options)
            ?? throw new InvalidOperationException("API 端点配置文件内容为空");

        // 打印加载的配置，重点检查 ResponseMapping 是否被正确填充
        LLog.Info($"[Api] 已加载 {_endpoints.Count} 个端点配置");
        foreach (var kv in _endpoints)
        {
            LLog.Debug($"[Api] 端点 '{kv.Key}' -> ResponseMapping 条目数: {kv.Value.ResponseMapping.Count}");
            foreach (var map in kv.Value.ResponseMapping)
            {
                LLog.Debug($"    {map.Key} -> {map.Value}");
            }
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
        LLog.Debug($"[Api] 发起请求 [{endpointName}] {url}");

        for (int attempt = 0; attempt <= 20; attempt++)
        {
            try
            {
                var rawJson = await _http.GetStringAsync(url);
                LLog.Debug($"[Api] [{endpointName}] 原始响应: {rawJson}");

                var response = JsonSerializer.Deserialize<ApiModels<JsonObject>>(rawJson);
                if (response == null) continue;
                if (response.Code == 0 && response.Data != null)
                {
                    LLog.Debug($"[Api] [{endpointName}] 成功获取 data 节点，内容: {response.Data.ToJsonString()}");
                    return response.Data;
                }
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
                if (attempt > 0 || attempt == 20)
                    LLog.Debug($"[Api] [{endpointName}] 第 {attempt + 1}/21 次请求异常: {ex.Message}");
                continue;
            }
        }

        LLog.Error($"[Api] [{endpointName}] 重试次数已耗尽");
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

        LLog.Debug($"[Api] MapResponse 开始，端点: {endpointName}");
        LLog.Debug($"[Api] 数据节点内容: {data.ToJsonString()}");

        var result = new Dictionary<string, object?>();
        foreach (var kv in config.ResponseMapping)
        {
            LLog.Debug($"[Api] 尝试提取映射: {kv.Key} -> 路径 '{kv.Value}'");
            var node = GetPathNode(data, kv.Value);
            var value = ConvertJsonNodeToValue(node);
            LLog.Debug($"[Api] 提取结果: {(value == null ? "<null>" : value.ToString())}");
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
                JsonValueKind.Number => value.GetValue<long>(), // 如果数字可能是浮点数，可改用 decimal 或 double
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => value, // 保留为 JsonNode
                JsonValueKind.Array => value,
                _ => value.GetValue<object>() // fallback
            };
        }
        // 非 JsonValue 如 JsonObject/JsonArray 直接返回 node
        return node;
    }
}