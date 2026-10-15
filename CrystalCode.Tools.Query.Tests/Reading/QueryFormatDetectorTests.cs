using CrystalCode.Tools.Query.Reading;

namespace CrystalCode.Tools.Query.Tests.Reading;

public sealed class QueryFormatDetectorTests
{
    [Theory]
    [InlineData("a.json", "Json")]
    [InlineData("A.JSON", "Json")]
    [InlineData("data.jsonc", "Json")]
    [InlineData("tool.csproj", "Xml")]
    [InlineData("Directory.props", "Xml")]
    [InlineData("tool.targets", "Xml")]
    [InlineData("data.xml", "Xml")]
    [InlineData("icon.svg", "Xml")]
    public void TryDetect_ByExtension(string path, string expected)
    {
        Assert.True(QueryFormatDetector.TryDetect(path, null, "{}", out var format, out _));

        Assert.Equal(expected, format.ToString());
    }

    [Fact]
    public void TryDetect_YamlExtension_FailsWithTeachingMessage()
    {
        Assert.False(QueryFormatDetector.TryDetect("compose.yml", null, "a: 1", out _, out var error));

        Assert.Contains("YAML is not supported", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDetect_JsonContent_ForUnknownExtension()
    {
        Assert.True(QueryFormatDetector.TryDetect("data.txt", null, """  {"a": 1}""", out var format, out _));

        Assert.Equal(QueryFormat.Json, format);
    }

    [Fact]
    public void TryDetect_XmlContent_ForUnknownExtension()
    {
        Assert.True(QueryFormatDetector.TryDetect("data", null, "\n  <a/>", out var format, out _));

        Assert.Equal(QueryFormat.Xml, format);
    }

    [Fact]
    public void TryDetect_UnknownContent_Fails()
    {
        Assert.False(QueryFormatDetector.TryDetect("data.txt", null, "hello world", out _, out var error));

        Assert.Contains("was not detected", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDetect_ExplicitFormat_Wins()
    {
        Assert.True(QueryFormatDetector.TryDetect("compose.yaml", QueryFormat.Xml, "a: 1", out var format, out _));

        Assert.Equal(QueryFormat.Xml, format);
    }
}
