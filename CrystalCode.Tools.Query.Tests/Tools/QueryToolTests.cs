using System.Text;
using System.Text.Json;

using Crystal.Tools;

using CrystalCode.Tools.Query.Tools;

namespace CrystalCode.Tools.Query.Tests.Tools;

public sealed class QueryToolTests
{
    private const string Csproj =
        """
        <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Alpha" Version="1.0.0" />
          </ItemGroup>
          <ItemGroup>
            <PackageReference Include="Beta" Version="2.0.0" />
          </ItemGroup>
          <ItemGroup>
            <PackageReference Include="Gamma" Version="3.0.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void Definition_ReportsTheQueryTool()
    {
        var definition = new QueryTool().Definition;

        Assert.Equal("query", definition.Name);
        Assert.Contains(
            definition.InputSchema.GetProperty("required").EnumerateArray(),
            item => item.GetString() == "path");
        Assert.NotNull(definition.Description);
        Assert.Contains("JSON Pointer", definition.Description, StringComparison.Ordinal);
        Assert.Contains("YAML is not supported", definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonScalar_ReturnsTheRawValueText()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("package.json", """{"name":"x","version":0.10}""");

        var name = await QueryAsync(path, selector: "/name");
        var version = await QueryAsync(path, selector: "/version");

        Assert.Equal(ToolResultStatus.Success, name.Status);
        Assert.Equal("x", name.Text);
        Assert.Equal(ToolResultStatus.Success, version.Status);
        Assert.Equal("0.10", version.Text);
    }

    [Fact]
    public async Task JsonWholeDocument_IsPrettyPrinted()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("data.json", """{"a":{"b":1}}""");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("{\n  \"a\": {\n    \"b\": 1\n  }\n}", output.Text);
    }

    [Fact]
    public async Task JsonArray_PagesWithOffsetAndLimit()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("numbers.json", "[0,1,2,3,4,5,6,7,8,9]");

        var output = await QueryAsync(path, selector: "", offset: 3, limit: 4);

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal(
            "[\n  2,\n  3,\n  4,\n  5\n]\n[showing items 3-6 of 10; continue with offset 7]",
            output.Text);
    }

    [Fact]
    public async Task JsonObject_TooLarge_PrintsAStructurePreview()
    {
        using var temp = new TemporaryDirectory();
        var builder = new StringBuilder("{\n");
        for (var index = 0; index < 2000; index++)
        {
            if (index > 0)
            {
                builder.Append(",\n");
            }

            builder.Append($"  \"m{index:D4}\": \"{new string('x', 30)}\"");
        }

        builder.Append("\n}");
        var path = temp.WriteFile("big.json", builder.ToString());

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Contains("members and is too large to print", output.Text, StringComparison.Ordinal);
        Assert.Contains("m0000: string", output.Text, StringComparison.Ordinal);
        Assert.Contains("[use a deeper selector]", output.Text, StringComparison.Ordinal);
        Assert.True(output.Text.Length <= 50_000);
    }

    [Fact]
    public async Task JsonOffsetOnNonArray_Fails()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("data.json", """{"a":1}""");

        var output = await QueryAsync(path, offset: 1);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("page arrays", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonNullValue_ReturnsNull()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("data.json", """{"a":null}""");

        var output = await QueryAsync(path, selector: "/a");

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("null", output.Text);
    }

    [Fact]
    public async Task Jsonc_CommentsAndTrailingCommas_AreAccepted()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("data.jsonc", "{\n  // comment\n  \"a\": [1, 2,],\n}\n");

        var output = await QueryAsync(path, selector: "/a/1");

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("2", output.Text);
    }

    [Fact]
    public async Task FormatOverride_WinsForAnUnknownExtension()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("data.txt", """{"a":{"b":2}}""");

        var output = await QueryAsync(path, selector: "/a/b", format: "json");

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("2", output.Text);
    }

    [Fact]
    public async Task XmlText_SelectsTheValue()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("tool.csproj", Csproj);

        var output = await QueryAsync(path, selector: "/Project/PropertyGroup/TargetFramework/text()");

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("net10.0", output.Text);
    }

    [Fact]
    public async Task XmlAttribute_SelectsTheValue()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("tool.csproj", Csproj);

        var output = await QueryAsync(path, selector: "/Project/@Sdk");

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("Microsoft.NET.Sdk", output.Text);
    }

    [Fact]
    public async Task XmlMultipleMatches_AreNumberedAndPaged()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("tool.csproj", Csproj);

        var all = await QueryAsync(path, selector: "/Project/ItemGroup");
        var page = await QueryAsync(path, selector: "/Project/ItemGroup", offset: 2, limit: 1);

        Assert.Equal(ToolResultStatus.Success, all.Status);
        Assert.Contains("# 1", all.Text, StringComparison.Ordinal);
        Assert.Contains("# 3", all.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("continue with offset", all.Text, StringComparison.Ordinal);

        Assert.Equal(ToolResultStatus.Success, page.Status);
        Assert.Contains("# 2", page.Text, StringComparison.Ordinal);
        Assert.Contains("Include=\"Beta\"", page.Text, StringComparison.Ordinal);
        Assert.Contains(
            "showing matches 2-2 of 3; continue with offset 3",
            page.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task XmlUnknownRoot_TeachesTheDocumentElement()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("tool.csproj", Csproj);

        var output = await QueryAsync(path, selector: "/Nope");

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("document element is 'Project'", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_MissingFile()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "gone.json");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("was not found", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_DirectoryPath()
    {
        using var temp = new TemporaryDirectory();

        var output = await QueryAsync(temp.Path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("is a directory", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_BinaryFile()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteBytes("blob.bin", [0x50, 0x00, 0x51]);

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("looks binary", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_OversizedFile()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "big.json");
        using (var stream = File.Create(path))
        {
            stream.SetLength(QueryLimits.MaximumFileBytes + 1);
        }

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("larger than", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_EmptyFile()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("empty.json", "");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("is empty", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_YamlFile()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("compose.yml", "services:\n  web:\n    image: nginx");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("YAML is not supported", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_UnknownFormat()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("notes.txt", "hello world");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("was not detected", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_InvalidJson_NamesTheLine()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile("broken.json", "{\n  \"a\":\n}");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("Invalid JSON at line", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_Doctype_IsProhibited()
    {
        using var temp = new TemporaryDirectory();
        var path = temp.WriteFile(
            "doc.xml",
            """<!DOCTYPE foo [<!ENTITY x "y">]><foo>&x;</foo>""");

        var output = await QueryAsync(path);

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("Invalid XML at line", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_UnknownArgument()
    {
        var output = await new QueryTool().InvokeAsync(
            new ToolCall("1", "query", """{"path":"a.json","wat":1}"""));

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Contains("contain only path", output.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_CanceledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new QueryTool().InvokeAsync(
                new ToolCall("1", "query", """{"path":"a.json"}"""),
                cancellation.Token));
    }

    private static async Task<ToolOutput> QueryAsync(
        string path,
        string? selector = null,
        string? format = null,
        int? offset = null,
        int? limit = null)
    {
        var payload = new Dictionary<string, object?> { ["path"] = path };
        if (selector is not null)
        {
            payload["selector"] = selector;
        }

        if (format is not null)
        {
            payload["format"] = format;
        }

        if (offset is not null)
        {
            payload["offset"] = offset;
        }

        if (limit is not null)
        {
            payload["limit"] = limit;
        }

        var arguments = JsonSerializer.Serialize(payload);
        return await new QueryTool().InvokeAsync(new ToolCall("1", "query", arguments));
    }
}
