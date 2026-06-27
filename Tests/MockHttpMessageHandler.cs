using System.Text;

namespace BiliLiveNotifier.Tests;

/// <summary>
/// 测试用 HttpMessageHandler — 返回预设的 JSON 响应，不发起真实 HTTP 请求。
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(string jsonResponse)
        : this(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        })
    {
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// 构造一个返回 B 站标准格式 JSON 的 mock handler
    /// </summary>
    public static MockHttpMessageHandler ForBiliApi(string dataJson)
    {
        var fullJson = $$"""{"code":0,"message":"","data":{{dataJson}}}""";
        return new MockHttpMessageHandler(fullJson);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
