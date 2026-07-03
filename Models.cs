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

/// <summary>
/// 应用程序强类型配置模型
/// </summary>
public class NotifierConfig
{
    public long[] Uids { get; set; } = [];
    public int CheckInterval { get; set; } = 15;
    public int LiveCheckInterval { get; set; } = 45;
    public bool BirthdayText { get; set; } = true;
    public bool FilterBirthday { get; set; } = true;
    public bool AutoStart { get; set; } = false;

    /// <summary>
    /// 从 JsonNode 解析为强类型配置
    /// </summary>
    public static NotifierConfig FromJsonNode(JsonNode? node)
    {
        if (node is null) return new NotifierConfig();

        return new NotifierConfig
        {
            Uids = node["uids"]?.AsArray()
                ?.Select(v => v is not null ? (long)v : 0L)
                .ToArray() ?? [],
            CheckInterval = node["check_interval"]?.GetValue<int>() ?? 15,
            LiveCheckInterval = node["live_check_interval"]?.GetValue<int>() ?? 45,
            BirthdayText = node["birthday_text"]?.GetValue<bool>() ?? true,
            FilterBirthday = node["skip_default_birthday"]?.GetValue<bool>() ?? true,
            AutoStart = node["auto_start"]?.GetValue<bool>() ?? false,
        };
    }
}