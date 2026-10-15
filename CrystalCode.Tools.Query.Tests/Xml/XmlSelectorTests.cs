using System.Xml.Linq;

using CrystalCode.Tools.Query.Xml;

namespace CrystalCode.Tools.Query.Tests.Xml;

public sealed class XmlSelectorTests
{
    private const string Csproj =
        """
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Alpha" Version="1.0.0" />
            <PackageReference Include="Beta" Version="2.0.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void Select_DefaultNamespace_MatchesByLocalName()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("/Project/PropertyGroup/TargetFramework/text()", out var path, out _));

        var selection = XmlSelector.Select(document, path);

        Assert.Equal(XmlSelectionKind.Values, selection.Kind);
        Assert.Equal("net10.0", Assert.Single(selection.Values));
    }

    [Fact]
    public void Select_Descendant_FindsMatchesAtAnyDepth()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("//PackageReference", out var path, out _));

        var selection = XmlSelector.Select(document, path);

        Assert.Equal(XmlSelectionKind.Elements, selection.Kind);
        Assert.Equal(2, selection.Elements.Count);
    }

    [Fact]
    public void Select_Index_TakesTheNthMatch()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("//PackageReference[2]", out var path, out _));

        var selection = XmlSelector.Select(document, path);

        var element = Assert.Single(selection.Elements);
        Assert.Equal("Beta", element.Attribute("Include")!.Value);
    }

    [Fact]
    public void Select_Attribute_ReadsByLocalName()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("//PackageReference[1]/@Version", out var path, out _));

        var selection = XmlSelector.Select(document, path);

        Assert.Equal("1.0.0", Assert.Single(selection.Values));
    }

    [Fact]
    public void Select_Wildcard_MatchesEveryChild()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("/Project/*", out var path, out _));

        var selection = XmlSelector.Select(document, path);

        Assert.Equal(2, selection.Elements.Count);
    }

    [Fact]
    public void Select_PrefixedDocument_IgnoresThePrefix()
    {
        var document = XDocument.Parse(
            """<a:Project xmlns:a="urn:example"><a:Thing>v</a:Thing></a:Project>""");
        Assert.True(XmlPathParser.TryParse("/Project/Thing/text()", out var path, out _));

        var selection = XmlSelector.Select(document, path);

        Assert.Equal("v", Assert.Single(selection.Values));
    }

    [Fact]
    public void Select_UnknownRoot_TeachesTheDocumentElement()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("/Nope", out var path, out _));

        var exception = Assert.Throws<QueryException>(() => XmlSelector.Select(document, path));

        Assert.Contains("document element is 'Project'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_UnknownChild_ListsAvailableChildren()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("/Project/Nope", out var path, out _));

        var exception = Assert.Throws<QueryException>(() => XmlSelector.Select(document, path));

        Assert.Contains("No element matches 'Nope'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Available child elements", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_MissingAttribute_ListsAvailableAttributes()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("/Project/@Id", out var path, out _));

        var exception = Assert.Throws<QueryException>(() => XmlSelector.Select(document, path));

        Assert.Contains("No attribute '@Id'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_IndexPastTheEnd_ReportsTheMatchCount()
    {
        var document = XDocument.Parse(Csproj);
        Assert.True(XmlPathParser.TryParse("//PackageReference[5]", out var path, out _));

        var exception = Assert.Throws<QueryException>(() => XmlSelector.Select(document, path));

        Assert.Contains("matched 2 element(s)", exception.Message, StringComparison.Ordinal);
    }
}
