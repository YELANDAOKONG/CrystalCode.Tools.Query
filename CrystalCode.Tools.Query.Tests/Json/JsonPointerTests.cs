using System.Text.Json.Nodes;

using CrystalCode.Tools.Query.Json;

namespace CrystalCode.Tools.Query.Tests.Json;

public sealed class JsonPointerTests
{
    [Fact]
    public void TryParse_Empty_SelectsTheRoot()
    {
        Assert.True(JsonPointer.TryParse("", out var tokens, out var error));

        Assert.Empty(tokens);
        Assert.Null(error);
    }

    [Fact]
    public void TryParse_UnescapesTildeAndSlash()
    {
        Assert.True(JsonPointer.TryParse("/a~1b/c~0d", out var tokens, out _));

        Assert.Equal(["a/b", "c~d"], tokens);
    }

    [Fact]
    public void TryParse_SingleSlash_IsOneEmptyMember()
    {
        Assert.True(JsonPointer.TryParse("/", out var tokens, out _));

        Assert.Equal([""], tokens);
    }

    [Fact]
    public void TryParse_InvalidEscape_Fails()
    {
        Assert.False(JsonPointer.TryParse("/x~2", out _, out var error));

        Assert.Contains("invalid '~' escape", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_MissingLeadingSlash_Fails()
    {
        Assert.False(JsonPointer.TryParse("items", out _, out var error));

        Assert.Contains("starting with '/'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySelect_ArrayIndex_IsZeroBased()
    {
        var root = JsonNode.Parse("""{"items":[10,20,30]}""");

        Assert.True(JsonPointer.TryParse("/items/1", out var tokens, out _));
        Assert.True(JsonPointer.TrySelect(root, tokens, out var selected, out _));
        Assert.Equal(20, selected!.GetValue<int>());
    }

    [Fact]
    public void TrySelect_LeadingZeroIndex_Fails()
    {
        var root = JsonNode.Parse("""{"items":[10,20]}""");

        Assert.True(JsonPointer.TryParse("/items/01", out var tokens, out _));
        Assert.False(JsonPointer.TrySelect(root, tokens, out _, out var error));
        Assert.Contains("leading zero", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySelect_IndexOutOfRange_Fails()
    {
        var root = JsonNode.Parse("""{"items":[10,20]}""");

        Assert.True(JsonPointer.TryParse("/items/9", out var tokens, out _));
        Assert.False(JsonPointer.TrySelect(root, tokens, out _, out var error));
        Assert.Contains("out of range", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySelect_MinusIndex_Fails()
    {
        var root = JsonNode.Parse("""{"items":[10]}""");

        Assert.True(JsonPointer.TryParse("/items/-", out var tokens, out _));
        Assert.False(JsonPointer.TrySelect(root, tokens, out _, out var error));
        Assert.Contains("'-' index", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySelect_MissingMember_ListsAvailableMembers()
    {
        var root = JsonNode.Parse("""{"a":1,"b":2}""");

        Assert.True(JsonPointer.TryParse("/c", out var tokens, out _));
        Assert.False(JsonPointer.TrySelect(root, tokens, out _, out var error));
        Assert.Contains("Available members: a, b", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySelect_DescendsIntoAScalar_Fails()
    {
        var root = JsonNode.Parse("""{"a":1}""");

        Assert.True(JsonPointer.TryParse("/a/b", out var tokens, out _));
        Assert.False(JsonPointer.TrySelect(root, tokens, out _, out var error));
        Assert.Contains("descends into a value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySelect_NullMember_SelectsNull()
    {
        var root = JsonNode.Parse("""{"a":null}""");

        Assert.True(JsonPointer.TryParse("/a", out var tokens, out _));
        Assert.True(JsonPointer.TrySelect(root, tokens, out var selected, out _));
        Assert.Null(selected);
    }

    [Fact]
    public void TrySelect_WholeDocument_SelectsTheRoot()
    {
        var root = JsonNode.Parse("""{"a":1}""");

        Assert.True(JsonPointer.TryParse("", out var tokens, out _));
        Assert.True(JsonPointer.TrySelect(root, tokens, out var selected, out _));
        Assert.Same(root, selected);
    }
}
