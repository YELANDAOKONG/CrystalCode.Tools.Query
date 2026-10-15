using System.Text.Json;

using CrystalCode.Tools.Query.Reading;

namespace CrystalCode.Tools.Query.Tools;

/// <summary>
/// The query tool arguments.
/// </summary>
/// <param name="Path">The file to read.</param>
/// <param name="Selector">The JSON Pointer or XML path; null selects the whole document.</param>
/// <param name="Format">The explicit format override; null detects it.</param>
/// <param name="Offset">The 1-based first item for arrays and match lists.</param>
/// <param name="Limit">The maximum array or match items to return.</param>
internal sealed record QueryArguments(
    string Path,
    string? Selector,
    QueryFormat? Format,
    int? Offset,
    int? Limit)
{
    private static readonly string[] Allowed = ["path", "selector", "format", "offset", "limit"];

    public static bool TryRead(string json, out QueryArguments? arguments, out string? error)
    {
        arguments = null;
        error = null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = "Tool arguments are not valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Tool arguments must be a JSON object.";
                return false;
            }

            foreach (var property in root.EnumerateObject())
            {
                if (!Array.Exists(
                        Allowed,
                        name => string.Equals(name, property.Name, StringComparison.Ordinal)))
                {
                    error = "Tool arguments must contain only path, selector, format, offset, and limit.";
                    return false;
                }
            }

            if (!root.TryGetProperty("path", out var pathElement)
                || pathElement.ValueKind != JsonValueKind.String)
            {
                error = "Argument 'path' must be a non-empty string.";
                return false;
            }

            var path = pathElement.GetString()!.Trim();
            if (path.Length == 0)
            {
                error = "Argument 'path' must be a non-empty string.";
                return false;
            }

            if (!TryReadSelector(root, out var selector, out error)
                || !TryReadFormat(root, out var format, out error)
                || !TryReadOffset(root, out var offset, out error)
                || !TryReadLimit(root, out var limit, out error))
            {
                return false;
            }

            arguments = new QueryArguments(path, selector, format, offset, limit);
            return true;
        }
    }

    private static bool TryReadSelector(JsonElement root, out string? selector, out string? error)
    {
        selector = null;
        error = null;
        if (!root.TryGetProperty("selector", out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            error = "Argument 'selector' must be a string.";
            return false;
        }

        var text = element.GetString()!.Trim();
        selector = text.Length > 0 ? text : null;
        return true;
    }

    private static bool TryReadFormat(JsonElement root, out QueryFormat? format, out string? error)
    {
        format = null;
        error = null;
        if (!root.TryGetProperty("format", out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString()!.Trim();
            if (string.Equals(text, "json", StringComparison.Ordinal))
            {
                format = QueryFormat.Json;
                return true;
            }

            if (string.Equals(text, "xml", StringComparison.Ordinal))
            {
                format = QueryFormat.Xml;
                return true;
            }
        }

        error = "Argument 'format' must be 'json' or 'xml'.";
        return false;
    }

    private static bool TryReadOffset(JsonElement root, out int? offset, out string? error)
    {
        offset = null;
        error = null;
        if (!root.TryGetProperty("offset", out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out var value)
            || value < 1)
        {
            error = "Argument 'offset' must be a positive integer.";
            return false;
        }

        offset = value;
        return true;
    }

    private static bool TryReadLimit(JsonElement root, out int? limit, out string? error)
    {
        limit = null;
        error = null;
        if (!root.TryGetProperty("limit", out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out var value)
            || value < 1
            || value > QueryLimits.MaximumPageItems)
        {
            error = $"Argument 'limit' must be an integer between 1 and {QueryLimits.MaximumPageItems}.";
            return false;
        }

        limit = value;
        return true;
    }
}
