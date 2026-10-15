using System.Text.Json;

using CrystalCode.Tools.Query.Tools;

namespace CrystalCode.Tools.Query.Tests;

public sealed class ManifestTests
{
    [Fact]
    public void ShipsAManifestThatMatchesTheTool()
    {
        var path = Path.Combine(FindRepositoryRoot(), "tools.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal("dotnet", root.GetProperty("runner").GetString());
        Assert.Equal("CrystalCode.Tools.Query.dll", root.GetProperty("assembly").GetString());
        Assert.Equal(
            [typeof(QueryTool).FullName!],
            root.GetProperty("types").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal(
            ["plan", "work"],
            root.GetProperty("catalogs").EnumerateArray().Select(item => item.GetString()!).ToArray());
        Assert.Equal("always", root.GetProperty("approval").GetString());
        Assert.True(root.GetProperty("timeoutSeconds").GetInt32() > 0);
        Assert.Equal(
            ["path"],
            root.GetProperty("tools")
                .GetProperty("query")
                .GetProperty("pathArguments")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !(File.Exists(Path.Combine(directory.FullName, "tools.json"))
                && File.Exists(Path.Combine(directory.FullName, "CrystalCode.Tools.Query.sln"))))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
