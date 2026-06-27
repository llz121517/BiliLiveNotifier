namespace BiliLiveNotifier.Tests.Core;

public class JsonNodeExtensionsTests
{
    [Fact]
    public void GetPath_SingleKey_ReturnsValue()
    {
        var json = JsonNode.Parse("""{"a":42}""");
        var result = json.GetPath("a");
        Assert.NotNull(result);
        Assert.Equal(42, result!.GetValue<int>());
    }

    [Fact]
    public void GetPath_NestedKeys_ReturnsValue()
    {
        var json = JsonNode.Parse("""{"a":{"b":{"c":"hello"}}}""");
        var result = json.GetPath("a", "b", "c");
        Assert.NotNull(result);
        Assert.Equal("hello", result!.GetValue<string>());
    }

    [Fact]
    public void GetPath_KeyNotFound_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"a":1}""");
        var result = json.GetPath("b");
        Assert.Null(result);
    }

    [Fact]
    public void GetPath_NestedKeyNotFound_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"a":{"b":1}}""");
        var result = json.GetPath("a", "x");
        Assert.Null(result);
    }

    [Fact]
    public void GetPath_DeepNestedKeyNotFound_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"a":{"b":{"c":1}}}""");
        var result = json.GetPath("a", "b", "x");
        Assert.Null(result);
    }

    [Fact]
    public void GetPath_NullNode_ReturnsNull()
    {
        JsonNode? nullNode = null;
        var result = nullNode.GetPath("a");
        Assert.Null(result);
    }

    [Fact]
    public void GetPath_IntermediateNodeIsValue_ReturnsNull()
    {
        // a 是数值不是对象，所以继续取 a.b 应返回 null
        var json = JsonNode.Parse("""{"a":42}""");
        var result = json.GetPath("a", "b");
        Assert.Null(result);
    }

    [Fact]
    public void GetPath_ArrayNode_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"a":[1,2,3]}""");
        var result = json.GetPath("a", "b");
        Assert.Null(result);
    }

    [Fact]
    public void GetPath_EmptyKeys_ReturnsOriginalNode()
    {
        var json = JsonNode.Parse("""{"a":1}""");
        var result = json.GetPath();
        Assert.Same(json, result);
    }

    [Fact]
    public void GetPath_NullIntermediateNode_ReturnsNull()
    {
        var json = JsonNode.Parse("""{"a":null}""");
        var result = json.GetPath("a", "b");
        Assert.Null(result);
    }
}
