using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

using Crystal.Tools;

using CrystalCode.Tools.Query.Reading;
using CrystalCode.Tools.Query.Rendering;
using CrystalCode.Tools.Query.Selectors.Json;
using CrystalCode.Tools.Query.Selectors.Xml;

namespace CrystalCode.Tools.Query.Tools;

/// <summary>
/// Returns one selected part of one JSON or XML file.
/// </summary>
public sealed class QueryTool : ITool
{
    private const string ToolName = "query";

    private const string ToolDescription =
        "Queries one JSON or XML file and returns the selected part. "
        + "Use it for large, minified, or single-line structured files such as package.json, "
        + "lock files, project files, and generated XML; use read, grep, or search for ordinary source files. "
        + "The selector dialect follows the detected format. "
        + "JSON uses an RFC 6901 JSON Pointer such as /scripts/build or /items/0 (0-based); "
        + "the empty selector means the whole document. "
        + "XML uses an element path such as /Project/PropertyGroup/TargetFramework, //PackageReference, "
        + "child[2] for the nth match (1-based), @Version for an attribute value, or text() for all text "
        + "inside the element, trimmed; "
        + "matching ignores namespaces. "
        + "The format is detected from the path extension (.json, .jsonc, .xml, .csproj, .props, .targets, "
        + ".vbproj, .fsproj, .plist, .svg, .xaml, .resx, .config) or from the content; format overrides the detection. "
        + "YAML is not supported. "
        + "offset and limit page arrays and match lists; limit defaults to 50 and is capped at 200. "
        + "A selector that finds nothing fails with the surrounding structure. "
        + "An object that repeats a member name is rejected with the repeated member's pointer. "
        + "Paths outside the workspace require approval.";

    private const string Schema =
        """
        {
          "type": "object",
          "properties": {
            "path": {
              "type": "string",
              "description": "Workspace-relative or absolute path to a JSON or XML file."
            },
            "selector": {
              "type": "string",
              "description": "JSON: an RFC 6901 JSON Pointer such as /scripts/build; empty means the whole document. XML: a path such as /Project/PropertyGroup, //Item, child[2], @name, or text()."
            },
            "format": {
              "type": "string",
              "enum": ["json", "xml"],
              "description": "Override the detected format."
            },
            "offset": {
              "type": "integer",
              "minimum": 1,
              "description": "1-based item to start from when the selection is an array or match list; optional."
            },
            "limit": {
              "type": "integer",
              "minimum": 1,
              "maximum": 200,
              "description": "Maximum array or match items to return (1-200, default 50); optional."
            }
          },
          "required": ["path"],
          "additionalProperties": false
        }
        """;

    private static readonly ToolDefinition Tool = CreateTool();

    /// <inheritdoc />
    public ToolDefinition Definition => Tool;

    /// <inheritdoc />
    public async ValueTask<ToolOutput> InvokeAsync(
        ToolCall call,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (!QueryArguments.TryRead(call.Arguments, out var arguments, out var error)
            || arguments is null)
        {
            return Failure(error ?? "Tool arguments are not valid.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var (text, readError) = await QueryFileReader
            .ReadAsync(arguments.Path, cancellationToken)
            .ConfigureAwait(false);
        if (text is null)
        {
            return Failure(readError ?? "The file could not be read.");
        }

        if (!QueryFormatDetector.TryDetect(arguments.Path, arguments.Format, text, out var format, out error))
        {
            return Failure(error ?? "The file format was not detected.");
        }

        try
        {
            var output = format switch
            {
                QueryFormat.Json => QueryJson(text, arguments),
                QueryFormat.Xml => QueryXml(text, arguments),
                _ => throw new QueryException("The file format is not supported.")
            };
            return new ToolOutput(output);
        }
        catch (QueryException exception)
        {
            return Failure(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure("The file could not be read.");
        }
    }

    private static string QueryJson(string text, QueryArguments arguments)
    {
        if (!JsonPointer.TryReadDocument(text, out var root, out var error))
        {
            throw new QueryException(error ?? "Invalid JSON.");
        }

        if (!JsonPointer.TryParse(arguments.Selector, out var tokens, out error))
        {
            throw new QueryException(error ?? "The JSON selector is not valid.");
        }

        if (!JsonPointer.TrySelect(root, tokens, out var selected, out error))
        {
            throw new QueryException(error ?? "The JSON selector did not match.");
        }

        return QueryRenderer.RenderJson(selected, arguments.Offset, arguments.Limit);
    }

    private static string QueryXml(string text, QueryArguments arguments)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true
            };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new QueryException(
                $"Invalid XML at line {exception.LineNumber}, "
                + $"position {exception.LinePosition}: {exception.Message}");
        }

        if (!XmlPathParser.TryParse(arguments.Selector, out var path, out var error))
        {
            throw new QueryException(error ?? "The XML selector is not valid.");
        }

        var selection = XmlSelector.Select(document, path);
        return QueryRenderer.RenderXml(selection, arguments.Offset, arguments.Limit);
    }

    private static ToolOutput Failure(string message) =>
        new(message, ToolResultStatus.Failure);

    private static ToolDefinition CreateTool()
    {
        using var document = JsonDocument.Parse(Schema);
        return new ToolDefinition(ToolName, document.RootElement.Clone(), ToolDescription);
    }
}
