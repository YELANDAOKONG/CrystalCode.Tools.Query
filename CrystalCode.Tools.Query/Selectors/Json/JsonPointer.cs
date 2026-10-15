using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrystalCode.Tools.Query.Selectors.Json;

/// <summary>
/// RFC 6901 JSON Pointer parsing and selection over a node tree.
/// </summary>
internal static class JsonPointer
{
    public static bool TryParse(string? selector, out string[] tokens, out string? error)
    {
        tokens = [];
        error = null;
        if (string.IsNullOrEmpty(selector))
        {
            return true;
        }

        if (!QueryLimits.IsSelectorLengthAllowed(selector, out error))
        {
            return false;
        }

        if (selector[0] != '/')
        {
            error = "A JSON selector must be an RFC 6901 JSON Pointer starting with '/', "
                + "or empty for the whole document.";
            return false;
        }

        var parts = selector.Split('/');
        var parsed = new string[parts.Length - 1];
        for (var index = 1; index < parts.Length; index++)
        {
            if (!TryUnescape(parts[index], out var token))
            {
                error = $"The selector segment '{parts[index]}' has an invalid '~' escape; "
                    + "use ~0 for ~ and ~1 for /.";
                return false;
            }

            parsed[index - 1] = token;
        }

        tokens = parsed;
        return true;
    }

    /// <summary>
    /// Parses one JSON document and rejects repeated object members up front,
    /// because a parsed JsonObject with duplicate keys refuses Count, lookup, and
    /// enumeration, which every later select, render, and preview path performs.
    /// </summary>
    /// <param name="text">The complete document text.</param>
    /// <param name="root">The parsed root, or null for the JSON literal null.</param>
    /// <param name="error">The failure message on invalid or ambiguous JSON.</param>
    public static bool TryReadDocument(string text, out JsonNode? root, out string? error)
    {
        root = null;
        error = null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                text,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = QueryLimits.MaximumJsonDepth
                });
        }
        catch (JsonException exception)
        {
            error = $"Invalid JSON at line {exception.LineNumber + 1}, "
                + $"position {exception.BytePositionInLine + 1}: {exception.Message}";
            return false;
        }

        using (document)
        {
            if (!TryFindDuplicate(document.RootElement, [], out var duplicatePath))
            {
                error = $"The document repeats the member '{BuildPath(duplicatePath!)}'; repeated members cannot be "
                    + "selected unambiguously. Remove the repeated member first.";
                return false;
            }
        }

        try
        {
            root = JsonNode.Parse(
                text,
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = QueryLimits.MaximumJsonDepth
                });
            return true;
        }
        catch (JsonException exception)
        {
            error = $"Invalid JSON at line {exception.LineNumber + 1}, "
                + $"position {exception.BytePositionInLine + 1}: {exception.Message}";
            return false;
        }
        catch (ArgumentException exception)
        {
            error = "Invalid JSON: " + exception.Message;
            return false;
        }
    }

    public static bool TrySelect(
        JsonNode? root,
        string[] tokens,
        out JsonNode? selected,
        out string? error)
    {
        var current = root;
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            switch (current)
            {
                case JsonObject target:
                    if (!target.TryGetPropertyValue(token, out var child))
                    {
                        error = $"The member '{token}' was not found. {DescribeMembers(target)}";
                        selected = null;
                        return false;
                    }

                    current = child;
                    break;

                case JsonArray target:
                {
                    if (!TryParseIndex(token, out var position, out error))
                    {
                        selected = null;
                        return false;
                    }

                    if (position >= target.Count)
                    {
                        error = $"The index {position} is out of range; the array has "
                            + $"{target.Count} items (0-based indexes).";
                        selected = null;
                        return false;
                    }

                    current = target[position];
                    break;
                }

                default:
                    error = $"The selector descends into a value at /{string.Join('/', tokens[..index])}; "
                        + $"that value is {Describe(current)}.";
                    selected = null;
                    return false;
            }
        }

        selected = current;
        error = null;
        return true;
    }

    private static bool TryUnescape(string raw, out string token)
    {
        token = raw;
        if (!raw.Contains('~'))
        {
            return true;
        }

        var builder = new StringBuilder(raw.Length);
        for (var index = 0; index < raw.Length; index++)
        {
            var character = raw[index];
            if (character != '~')
            {
                builder.Append(character);
                continue;
            }

            if (index + 1 >= raw.Length)
            {
                return false;
            }

            index++;
            var escape = raw[index];
            if (escape == '0')
            {
                builder.Append('~');
            }
            else if (escape == '1')
            {
                builder.Append('/');
            }
            else
            {
                return false;
            }
        }

        token = builder.ToString();
        return true;
    }

    private static bool TryParseIndex(string token, out int index, out string? error)
    {
        index = 0;
        error = null;
        if (token.Length == 0 || token == "-")
        {
            error = "The '-' index refers past the end of an array and cannot be read.";
            return false;
        }

        if (token.Length > 1 && token[0] == '0')
        {
            error = $"The index '{token}' has a leading zero; "
                + "array indexes are 0 or a positive integer without leading zeros.";
            return false;
        }

        foreach (var character in token)
        {
            if (character is < '0' or > '9')
            {
                error = $"The index '{token}' is not a decimal integer; "
                    + "array indexes are 0-based, for example /items/0.";
                return false;
            }
        }

        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index))
        {
            error = $"The index '{token}' is too large.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Scans the raw element tree for repeated object members. The scan reads the
    /// backing JsonElement, which keeps duplicates, so it never triggers the
    /// dictionary rebuild that makes a parsed duplicate-key JsonObject throw.
    /// </summary>
    /// <param name="element">The element to scan.</param>
    /// <param name="path">The member path segments built so far.</param>
    /// <param name="duplicatePath">The first repeated member's path segments.</param>
    private static bool TryFindDuplicate(
        JsonElement element,
        List<string> path,
        out List<string>? duplicatePath)
    {
        duplicatePath = null;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!seen.Add(property.Name))
                    {
                        duplicatePath = [.. path, property.Name];
                        return false;
                    }

                    path.Add(property.Name);
                    var unique = TryFindDuplicate(property.Value, path, out duplicatePath);
                    path.RemoveAt(path.Count - 1);
                    if (!unique)
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.Array:
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    path.Add(index.ToString(CultureInfo.InvariantCulture));
                    var unique = TryFindDuplicate(item, path, out duplicatePath);
                    path.RemoveAt(path.Count - 1);
                    if (!unique)
                    {
                        return false;
                    }

                    index++;
                }

                return true;
            }

            default:
                return true;
        }
    }

    private static string BuildPath(List<string> segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            builder.Append('/').Append(segment.Replace("~", "~0").Replace("/", "~1"));
        }

        return builder.ToString();
    }

    private static string DescribeMembers(JsonObject target)
    {
        if (target.Count == 0)
        {
            return "The object has no members.";
        }

        var names = new List<string>();
        foreach (var pair in target)
        {
            if (names.Count == 10)
            {
                names.Add($"and {target.Count - 10} more");
                break;
            }

            names.Add(pair.Key);
        }

        return "Available members: " + string.Join(", ", names) + ".";
    }

    private static string Describe(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject => "an object",
        JsonArray => "an array",
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            _ => "a value"
        },
        _ => "a value"
    };
}
