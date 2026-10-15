using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrystalCode.Tools.Query.Json;

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

        if (selector.Length > QueryLimits.MaximumSelectorCharacters)
        {
            error = "The selector is too long.";
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
