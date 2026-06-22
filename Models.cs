using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BiliLiveNotifier;

/// <summary>
/// B站 API 统一响应模型
/// </summary>
public class ApiModels<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;
}

/// <summary>
/// API 端点 JSON 配置映射模型
/// </summary>
public class ApiEndpointConfig
{
    public string Url { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public Dictionary<string, string> Params { get; set; } = new();

    public string Description { get; set; } = string.Empty;

    public Dictionary<string, string> ResponseMapping { get; set; } = new();
}

/// <summary>
/// JsonObject 安全路径取值扩展
/// </summary>
public static class JsonNodeExtensions
{
    public static JsonNode? GetPath(this JsonNode? node, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (node is not JsonObject obj) return null;
            if (!obj.TryGetPropertyValue(key, out node)) return null;
        }
        return node;
    }
}