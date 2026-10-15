namespace CrystalCode.Tools.Query.Tests;

/// <summary>
/// A temporary directory that removes itself on dispose.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "query-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string WriteFile(string name, string contents)
    {
        var fullPath = System.IO.Path.Combine(Path, name);
        File.WriteAllText(fullPath, contents);
        return fullPath;
    }

    public string WriteBytes(string name, byte[] contents)
    {
        var fullPath = System.IO.Path.Combine(Path, name);
        File.WriteAllBytes(fullPath, contents);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
