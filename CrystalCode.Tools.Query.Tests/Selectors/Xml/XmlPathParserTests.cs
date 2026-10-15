using CrystalCode.Tools.Query.Selectors.Xml;

namespace CrystalCode.Tools.Query.Tests.Selectors.Xml;

public sealed class XmlPathParserTests
{
    [Fact]
    public void TryParse_ChildPath_BuildsSteps()
    {
        Assert.True(XmlPathParser.TryParse("/a/b", out var path, out _));

        Assert.Equal(XmlTerminal.Element, path.Terminal);
        Assert.Equal(2, path.Steps.Count);
        Assert.Equal("a", path.Steps[0].Name);
        Assert.False(path.Steps[0].Descendant);
        Assert.Equal("b", path.Steps[1].Name);
        Assert.False(path.Steps[1].Descendant);
        Assert.Null(path.Steps[0].Index);
    }

    [Fact]
    public void TryParse_DescendantStep_MarksTheStep()
    {
        Assert.True(XmlPathParser.TryParse("//b", out var path, out _));

        Assert.True(Assert.Single(path.Steps).Descendant);
    }

    [Fact]
    public void TryParse_MixedDescendant_MarksOnlyTheSecondStep()
    {
        Assert.True(XmlPathParser.TryParse("/a//b", out var path, out _));

        Assert.False(path.Steps[0].Descendant);
        Assert.True(path.Steps[1].Descendant);
    }

    [Fact]
    public void TryParse_Index_IsOneBased()
    {
        Assert.True(XmlPathParser.TryParse("/a/b[2]", out var path, out _));

        Assert.Equal(2, path.Steps[1].Index);
    }

    [Fact]
    public void TryParse_AttributeTerminal_KeepsTheName()
    {
        Assert.True(XmlPathParser.TryParse("/a/@x", out var path, out _));

        Assert.Equal(XmlTerminal.Attribute, path.Terminal);
        Assert.Equal("x", path.TerminalName);
    }

    [Fact]
    public void TryParse_TextTerminal_IsRecognized()
    {
        Assert.True(XmlPathParser.TryParse("/a/b/text()", out var path, out _));

        Assert.Equal(XmlTerminal.Text, path.Terminal);
    }

    [Fact]
    public void TryParse_WildcardStep_IsAllowed()
    {
        Assert.True(XmlPathParser.TryParse("/a/*", out var path, out _));

        Assert.Equal("*", path.Steps[1].Name);
    }

    [Fact]
    public void TryParse_Root_IsTheWholeDocument()
    {
        Assert.True(XmlPathParser.TryParse("/", out var path, out _));

        Assert.Empty(path.Steps);
        Assert.Equal(XmlTerminal.Element, path.Terminal);
    }

    [Fact]
    public void TryParse_ZeroIndex_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/a/b[0]", out _, out var error));

        Assert.Contains("1-based positive integer", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_MissingLeadingSlash_Fails()
    {
        Assert.False(XmlPathParser.TryParse("text()", out _, out var error));

        Assert.Contains("must start with '/'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_TrailingSlash_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/a/", out _, out var error));

        Assert.Contains("ends with '/'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_TrailingDescendant_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/a//", out _, out var error));

        Assert.Contains("ends with '//'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_AttributeBeforeLastStep_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/a/@x/y", out _, out var error));

        Assert.Contains("must be the last step", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_BareTextTerminal_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/text()", out _, out var error));

        Assert.Contains("must follow at least one element step", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_UnsupportedPredicate_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/a[@x]", out _, out var error));

        Assert.Contains("is not supported", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_LongSelector_Fails()
    {
        var selector = "/" + new string('a', QueryLimits.MaximumSelectorCharacters);

        Assert.False(XmlPathParser.TryParse(selector, out _, out var error));

        Assert.Contains("too long", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_DoubleIndex_Fails()
    {
        Assert.False(XmlPathParser.TryParse("/a/b[1][2]", out _, out _));
    }
}
