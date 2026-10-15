using CrystalCode.Tools.Query.Reading;
using CrystalCode.Tools.Query.Tools;

namespace CrystalCode.Tools.Query.Tests.Tools;

public sealed class QueryArgumentsTests
{
    [Fact]
    public void TryRead_ReadsAllArguments()
    {
        Assert.True(QueryArguments.TryRead(
            """{"path":"  a.json  ","selector":" /x ","format":"json","offset":2,"limit":3}""",
            out var arguments,
            out _));

        Assert.NotNull(arguments);
        Assert.Equal("a.json", arguments.Path);
        Assert.Equal("/x", arguments.Selector);
        Assert.Equal(QueryFormat.Json, arguments.Format);
        Assert.Equal(2, arguments.Offset);
        Assert.Equal(3, arguments.Limit);
    }

    [Fact]
    public void TryRead_NullValues_AreDefaults()
    {
        Assert.True(QueryArguments.TryRead(
            """{"path":"a.json","selector":null,"format":null,"offset":null,"limit":null}""",
            out var arguments,
            out _));

        Assert.NotNull(arguments);
        Assert.Null(arguments.Selector);
        Assert.Null(arguments.Format);
        Assert.Null(arguments.Offset);
        Assert.Null(arguments.Limit);
    }

    [Fact]
    public void TryRead_EmptySelector_MeansWholeDocument()
    {
        Assert.True(QueryArguments.TryRead("""{"path":"a.json","selector":""}""", out var arguments, out _));

        Assert.Null(arguments!.Selector);
    }

    [Fact]
    public void TryRead_UnknownProperty_Fails()
    {
        Assert.False(QueryArguments.TryRead("""{"path":"a.json","wat":1}""", out _, out var error));

        Assert.Contains("contain only path", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_MissingPath_Fails()
    {
        Assert.False(QueryArguments.TryRead("{}", out _, out var error));

        Assert.Contains("'path'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_NonJson_Fails()
    {
        Assert.False(QueryArguments.TryRead("not json", out _, out var error));

        Assert.Contains("not valid JSON", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_BadFormat_Fails()
    {
        Assert.False(QueryArguments.TryRead("""{"path":"a.json","format":"JSON"}""", out _, out var error));

        Assert.Contains("'json' or 'xml'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_ZeroOffset_Fails()
    {
        Assert.False(QueryArguments.TryRead("""{"path":"a.json","offset":0}""", out _, out var error));

        Assert.Contains("positive integer", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_LimitOutOfRange_Fails()
    {
        Assert.False(QueryArguments.TryRead("""{"path":"a.json","limit":201}""", out _, out var error));

        Assert.Contains("between 1 and 200", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRead_MaximumLimit_IsAccepted()
    {
        Assert.True(QueryArguments.TryRead("""{"path":"a.json","limit":200}""", out var arguments, out _));

        Assert.Equal(200, arguments!.Limit);
    }
}
