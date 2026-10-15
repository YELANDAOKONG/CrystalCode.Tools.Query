using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace CrystalCode.Tools.Query.Rendering;

/// <summary>
/// Describes an oversized selection by its shape instead of its full text.
/// </summary>
internal static class StructurePreview
{
    public static string DescribeObject(JsonObject target)
    {
        var builder = new StringBuilder();
        builder.Append($"The selected object has {target.Count} members and is too large to print:");
        AppendMembers(builder, target);
        builder.Append("\n[use a deeper selector]");
        return builder.ToString();
    }

    public static string DescribeItem(JsonNode? item, int position, int total)
    {
        var builder = new StringBuilder($"Item {position} of {total} ");
        switch (item)
        {
            case JsonObject target:
                builder.Append($"is an object with {target.Count} members and is too large to print:");
                AppendMembers(builder, target);
                break;
            case JsonArray array:
                builder.Append($"is an array with {array.Count} items; page it with offset and limit.");
                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                builder.Append(
                    $"is a string with {value.GetValue<string>().Length} characters and is too large to print.");
                break;
            case JsonValue value:
                builder.Append($"is too large to print: {Clip(value.ToJsonString())}");
                break;
            default:
                builder.Append("is too large to print.");
                break;
        }

        builder.Append("\n[use a deeper selector or page with offset and limit]");
        return builder.ToString();
    }

    public static string DescribeElement(XElement element)
    {
        var attributes = element.Attributes().ToList();
        var children = element.Elements().ToList();
        var builder = new StringBuilder(
            $"The selected element <{element.Name.LocalName}> has {attributes.Count} attribute(s), "
            + $"{children.Count} child element(s), and is too large to print:");
        foreach (var attribute in attributes.Take(10))
        {
            builder.Append($"\n  @{attribute.Name.LocalName}: {Clip(attribute.Value)}");
        }

        var names = new List<string>();
        foreach (var child in children)
        {
            if (!names.Contains(child.Name.LocalName))
            {
                names.Add(child.Name.LocalName);
            }
        }

        if (names.Count > 0)
        {
            builder.Append("\n  child elements: ").Append(string.Join(", ", names.Take(12)));
        }

        builder.Append("\n[use a deeper selector]");
        return builder.ToString();
    }

    private static void AppendMembers(StringBuilder builder, JsonObject target)
    {
        var shown = 0;
        foreach (var pair in target)
        {
            if (shown == QueryLimits.MaximumPreviewMembers)
            {
                builder.Append($"\n  (and {target.Count - shown} more members)");
                break;
            }

            builder.Append("\n  ").Append(pair.Key).Append(": ").Append(DescribeValue(pair.Value));
            shown++;
        }
    }

    private static string DescribeValue(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject target => $"object ({target.Count} members)",
        JsonArray array => $"array ({array.Count} items)",
        JsonValue value => DescribeScalar(value),
        _ => "value"
    };

    private static string DescribeScalar(JsonValue value)
    {
        var kind = value.GetValueKind();
        if (kind == JsonValueKind.String)
        {
            var text = value.GetValue<string>();
            return text.Length <= 80
                ? "string " + JsonSerializer.Serialize(text)
                : $"string ({text.Length} characters)";
        }

        var raw = Clip(value.ToJsonString());
        return kind switch
        {
            JsonValueKind.Number => "number " + raw,
            JsonValueKind.True or JsonValueKind.False => "boolean " + raw,
            _ => raw
        };
    }

    private static string Clip(string text) => text.Length <= 80 ? text : text[..80] + "...";
}
