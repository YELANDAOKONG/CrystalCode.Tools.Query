using System.Xml.Linq;

namespace CrystalCode.Tools.Query.Xml;

/// <summary>
/// Evaluates a parsed XML path over one document with namespace-insensitive
/// matching: element and attribute names compare by local name.
/// </summary>
internal static class XmlSelector
{
    public static XmlSelection Select(XDocument document, XmlPath path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(path);
        if (document.Root is null)
        {
            throw new QueryException("The XML document has no root element.");
        }

        var current = new List<XElement> { document.Root };
        var shown = string.Empty;
        for (var index = 0; index < path.Steps.Count; index++)
        {
            var step = path.Steps[index];
            var matches = new List<XElement>();
            foreach (var element in current)
            {
                IEnumerable<XElement> candidates = index == 0
                    ? step.Descendant ? element.DescendantsAndSelf() : [element]
                    : step.Descendant ? element.Descendants() : element.Elements();
                foreach (var candidate in candidates)
                {
                    if (NameMatches(candidate, step.Name))
                    {
                        matches.Add(candidate);
                    }
                }
            }

            if (matches.Count == 0)
            {
                throw new QueryException(NoMatch(index, step, current, shown));
            }

            if (step.Index is int wanted)
            {
                if (matches.Count < wanted)
                {
                    throw new QueryException(
                        $"The step '{Describe(step)}' matched {matches.Count} element(s); "
                        + "the index is past the end.");
                }

                matches = [matches[wanted - 1]];
            }

            shown += "/" + Describe(step);
            current = matches;
        }

        return path.Terminal switch
        {
            XmlTerminal.Attribute => SelectAttribute(current, path.TerminalName!, shown),
            XmlTerminal.Text => XmlSelection.ForValues(
                [.. current.Select(element => element.Value.Trim())]),
            _ => XmlSelection.ForElements(current)
        };
    }

    private static bool NameMatches(XElement element, string name) =>
        string.Equals(name, "*", StringComparison.Ordinal)
        || string.Equals(element.Name.LocalName, name, StringComparison.Ordinal);

    private static string Describe(XmlPathStep step) =>
        step.Name + (step.Index is int position ? $"[{position}]" : string.Empty);

    private static string NoMatch(
        int index,
        XmlPathStep step,
        List<XElement> parents,
        string shown)
    {
        if (index == 0)
        {
            return $"The document element is '{parents[0].Name.LocalName}'; "
                + $"the selector starts with '{Describe(step)}'.";
        }

        var names = new List<string>();
        var extra = 0;
        foreach (var parent in parents)
        {
            foreach (var child in parent.Elements())
            {
                if (names.Contains(child.Name.LocalName))
                {
                    continue;
                }

                if (names.Count == 12)
                {
                    extra++;
                    continue;
                }

                names.Add(child.Name.LocalName);
            }
        }

        var available = names.Count == 0
            ? "It has no child elements."
            : "Available child elements: " + string.Join(", ", names)
                + (extra > 0 ? $" (and {extra} more)" : string.Empty) + ".";
        return $"No element matches '{Describe(step)}' at {shown}. {available}";
    }

    private static XmlSelection SelectAttribute(
        List<XElement> elements,
        string name,
        string shown)
    {
        var values = new List<string>();
        foreach (var element in elements)
        {
            foreach (var attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, name, StringComparison.Ordinal))
                {
                    values.Add(attribute.Value);
                }
            }
        }

        if (values.Count == 0)
        {
            var names = new List<string>();
            var extra = 0;
            foreach (var element in elements)
            {
                foreach (var attribute in element.Attributes())
                {
                    if (names.Contains(attribute.Name.LocalName))
                    {
                        continue;
                    }

                    if (names.Count == 12)
                    {
                        extra++;
                        continue;
                    }

                    names.Add(attribute.Name.LocalName);
                }
            }

            var available = names.Count == 0
                ? "It has no attributes."
                : "Available attributes: " + string.Join(", ", names)
                    + (extra > 0 ? $" (and {extra} more)" : string.Empty) + ".";
            throw new QueryException($"No attribute '@{name}' at {shown}. {available}");
        }

        return XmlSelection.ForValues(values);
    }
}
