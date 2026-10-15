using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using CrystalCode.Tools.Query.Selectors.Xml;

namespace CrystalCode.Tools.Query.Rendering;

/// <summary>
/// Renders one selection as model-facing text with paging and size caps.
/// </summary>
internal static class QueryRenderer
{
    public static string RenderJson(JsonNode? node, int? offset, int? limit)
    {
        switch (node)
        {
            case null:
                EnsureNoPaging(offset, limit, "a null value");
                return "null";
            case JsonArray array:
                return RenderJsonArray(array, offset, limit);
            case JsonObject target:
                EnsureNoPaging(offset, limit, "an object");
                return RenderJsonObject(target);
            case JsonValue value:
                EnsureNoPaging(offset, limit, "a single value");
                return RenderScalar(value);
            default:
                throw new QueryException("The selection could not be rendered.");
        }
    }

    public static string RenderXml(XmlSelection selection, int? offset, int? limit)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Kind switch
        {
            XmlSelectionKind.Values => RenderValues(selection.Values, offset, limit),
            _ => RenderElements(selection.Elements, offset, limit)
        };
    }

    private static string RenderJsonObject(JsonObject target)
    {
        var text = Write(target, 0);
        return text.Length <= QueryLimits.MaximumResultCharacters
            ? text
            : StructurePreview.DescribeObject(target);
    }

    private static string RenderJsonArray(JsonArray array, int? offset, int? limit)
    {
        var total = array.Count;
        if (total == 0)
        {
            return "[]";
        }

        var start = offset ?? 1;
        if (start > total)
        {
            throw new QueryException($"The array has {total} items; offset {start} is past the end.");
        }

        var window = Math.Min(limit ?? QueryLimits.DefaultPageItems, QueryLimits.MaximumPageItems);
        var end = Math.Min(total, start - 1 + window);
        var builder = new StringBuilder("[\n");
        var appended = 0;
        for (var index = start - 1; index < end; index++)
        {
            var item = Write(array[index], 2);
            if (builder.Length + item.Length + 4 > QueryLimits.MaximumResultCharacters)
            {
                if (appended == 0)
                {
                    return StructurePreview.DescribeItem(array[start - 1], start, total);
                }

                break;
            }

            if (appended > 0)
            {
                builder.Append(",\n");
            }

            builder.Append("  ").Append(item);
            appended++;
        }

        builder.Append("\n]");
        var last = start - 1 + appended;
        if (last < total)
        {
            builder.Append('\n')
                .Append($"[showing items {start}-{last} of {total}; continue with offset {last + 1}]");
        }

        return builder.ToString();
    }

    private static string RenderScalar(JsonValue value)
    {
        var kind = value.GetValueKind();
        var text = kind == JsonValueKind.String
            ? value.GetValue<string>()
            : value.ToJsonString();
        if (kind == JsonValueKind.String && text.Length == 0)
        {
            return "(empty string)";
        }

        if (text.Length > QueryLimits.MaximumScalarCharacters)
        {
            return text[..QueryLimits.MaximumScalarCharacters]
                + $"\n[truncated; {text.Length} characters total]";
        }

        return text;
    }

    private static string RenderElements(IReadOnlyList<XElement> elements, int? offset, int? limit)
    {
        var total = elements.Count;
        var start = offset ?? 1;
        if (start > total)
        {
            throw new QueryException($"The selector matched {total} element(s); offset {start} is past the end.");
        }

        var window = Math.Min(limit ?? QueryLimits.DefaultPageItems, QueryLimits.MaximumPageItems);
        var end = Math.Min(total, start - 1 + window);
        if (total == 1 && offset is null && limit is null)
        {
            var single = elements[0].ToString();
            return single.Length <= QueryLimits.MaximumResultCharacters
                ? single
                : StructurePreview.DescribeElement(elements[0]);
        }

        var builder = new StringBuilder();
        var appended = 0;
        for (var index = start - 1; index < end; index++)
        {
            var piece = elements[index].ToString();
            var header = total > 1 ? $"# {index + 1}\n" : string.Empty;
            if (builder.Length + header.Length + piece.Length + 2 > QueryLimits.MaximumResultCharacters)
            {
                if (appended == 0)
                {
                    return StructurePreview.DescribeElement(elements[start - 1]);
                }

                break;
            }

            if (appended > 0)
            {
                builder.Append('\n');
            }

            builder.Append(header).Append(piece);
            appended++;
        }

        var last = start - 1 + appended;
        if (last < total)
        {
            builder.Append('\n')
                .Append($"[showing matches {start}-{last} of {total}; continue with offset {last + 1}]");
        }

        return builder.ToString();
    }

    private static string RenderValues(IReadOnlyList<string> values, int? offset, int? limit)
    {
        var total = values.Count;
        var start = offset ?? 1;
        if (start > total)
        {
            throw new QueryException($"The selection has {total} values; offset {start} is past the end.");
        }

        var window = Math.Min(limit ?? QueryLimits.DefaultPageItems, QueryLimits.MaximumPageItems);
        var end = Math.Min(total, start - 1 + window);
        var builder = new StringBuilder();
        for (var index = start - 1; index < end; index++)
        {
            if (index > start - 1)
            {
                builder.Append('\n');
            }

            var value = values[index];
            if (value.Length == 0)
            {
                value = "(empty)";
            }
            else if (value.Length > QueryLimits.MaximumScalarCharacters)
            {
                value = value[..QueryLimits.MaximumScalarCharacters]
                    + $"\n[truncated; {values[index].Length} characters total]";
            }

            builder.Append(value);
            if (builder.Length > QueryLimits.MaximumResultCharacters)
            {
                builder.Length = QueryLimits.MaximumResultCharacters;
                builder.Append("\n[truncated; use offset and limit to page]");
                return builder.ToString();
            }
        }

        if (end < total)
        {
            builder.Append('\n')
                .Append($"[showing values {start}-{end} of {total}; continue with offset {end + 1}]");
        }

        return builder.ToString();
    }

    private static void EnsureNoPaging(int? offset, int? limit, string description)
    {
        if (offset is not null || limit is not null)
        {
            throw new QueryException($"offset and limit page arrays; the selected value is {description}.");
        }
    }

    private static string Write(JsonNode? node, int indent)
    {
        var builder = new StringBuilder();
        AppendNode(builder, node, indent);
        return builder.ToString();
    }

    private static void AppendNode(StringBuilder builder, JsonNode? node, int indent)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                break;

            case JsonObject target when target.Count == 0:
                builder.Append("{}");
                break;

            case JsonObject target:
            {
                builder.Append("{\n");
                var first = true;
                foreach (var pair in target)
                {
                    if (!first)
                    {
                        builder.Append(",\n");
                    }

                    first = false;
                    builder.Append(' ', indent + 2);
                    builder.Append(JsonSerializer.Serialize(pair.Key));
                    builder.Append(": ");
                    AppendNode(builder, pair.Value, indent + 2);
                }

                builder.Append('\n');
                builder.Append(' ', indent);
                builder.Append('}');
                break;
            }

            case JsonArray array when array.Count == 0:
                builder.Append("[]");
                break;

            case JsonArray array:
            {
                builder.Append("[\n");
                var first = true;
                foreach (var item in array)
                {
                    if (!first)
                    {
                        builder.Append(",\n");
                    }

                    first = false;
                    builder.Append(' ', indent + 2);
                    AppendNode(builder, item, indent + 2);
                }

                builder.Append('\n');
                builder.Append(' ', indent);
                builder.Append(']');
                break;
            }

            case JsonValue value:
                builder.Append(value.ToJsonString());
                break;
        }
    }
}
