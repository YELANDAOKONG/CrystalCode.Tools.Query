using System.Text.Json;

namespace CrystalCode.Tools.Query.Reading;

/// <summary>
/// Chooses the document format from the format argument, the path extension,
/// or the leading content.
/// </summary>
internal static class QueryFormatDetector
{
    private static readonly string[] JsonExtensions = [".json", ".jsonc"];
    private static readonly string[] XmlExtensions =
    [
        ".xml", ".csproj", ".props", ".targets", ".vbproj", ".fsproj",
        ".plist", ".svg", ".xaml", ".resx", ".config"
    ];
    private static readonly string[] YamlExtensions = [".yaml", ".yml"];

    public static bool TryDetect(
        string path,
        QueryFormat? requested,
        string text,
        out QueryFormat format,
        out string? error)
    {
        format = default;
        error = null;
        if (requested is { } chosen)
        {
            format = chosen;
            return true;
        }

        var extension = GetExtension(path);
        if (Contains(YamlExtensions, extension))
        {
            error = "YAML is not supported; use read, grep, or bash for YAML files.";
            return false;
        }

        if (Contains(JsonExtensions, extension))
        {
            format = QueryFormat.Json;
            return true;
        }

        if (Contains(XmlExtensions, extension))
        {
            format = QueryFormat.Xml;
            return true;
        }

        var start = 0;
        while (start < text.Length && (char.IsWhiteSpace(text[start]) || text[start] == '\uFEFF'))
        {
            start++;
        }

        if (start < text.Length && text[start] == '<')
        {
            format = QueryFormat.Xml;
            return true;
        }

        if (TryParseJson(text))
        {
            format = QueryFormat.Json;
            return true;
        }

        error = "The file format was not detected; pass format 'json' or 'xml'.";
        return false;
    }

    private static bool TryParseJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(
                text,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetExtension(string path)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static bool Contains(string[] values, string value) =>
        Array.Exists(values, item => string.Equals(item, value, StringComparison.Ordinal));
}
