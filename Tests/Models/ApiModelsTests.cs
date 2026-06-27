namespace BiliLiveNotifier.Tests.Models;

public class ApiModelsTests
{
    [Fact]
    public void Deserialize_SuccessResponse_ReturnsData()
    {
        var json = """{"code":0,"message":"","data":{"mid":123}}""";
        var result = JsonSerializer.Deserialize<ApiModels<JsonObject>>(json);

        Assert.NotNull(result);
        Assert.Equal(0, result.Code);
        Assert.Equal("", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal((long)123, result.Data["mid"]!.GetValue<long>());
    }

    [Fact]
    public void Deserialize_ErrorResponse_HasCodeAndMessage()
    {
        var json = """{"code":-400,"message":"请求错误","data":null}""";
        var result = JsonSerializer.Deserialize<ApiModels<JsonObject>>(json);

        Assert.NotNull(result);
        Assert.Equal(-400, result.Code);
        Assert.Equal("请求错误", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Deserialize_WithNestedData_ParsesCorrectly()
    {
        var json = """
        {
            "code": 0,
            "message": "",
            "data": {
                "info": { "uid": 496751305, "uname": "测试用户" },
                "room_id": 12345,
                "medal_name": "测试勋章"
            }
        }
        """;

        var result = JsonSerializer.Deserialize<ApiModels<JsonObject>>(json);

        Assert.NotNull(result);
        Assert.Equal(0, result.Code);

        var info = result.Data!["info"]!.AsObject();
        Assert.Equal((long)496751305, info["uid"]!.GetValue<long>());
        Assert.Equal("测试用户", info["uname"]!.GetValue<string>());
        Assert.Equal((long)12345, result.Data["room_id"]!.GetValue<long>());
    }

    [Fact]
    public void Deserialize_EmptyJson_Throws()
    {
        var json = "{}";
        var result = JsonSerializer.Deserialize<ApiModels<JsonObject>>(json);

        // code 默认为 0，data 默认为 null
        Assert.NotNull(result);
        Assert.Equal(0, result.Code);
        Assert.Null(result.Data);
    }
}
