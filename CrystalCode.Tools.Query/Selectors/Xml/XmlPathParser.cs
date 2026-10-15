using System.Globalization;

namespace CrystalCode.Tools.Query.Selectors.Xml;

/// <summary>
/// Parses an XML selector into steps: /a/b, //b, *, name[n], @name, and text().
/// </summary>
internal static class XmlPathParser
{
    public static bool TryParse(string? selector, out XmlPath path, out string? error)
    {
        var steps = new List<XmlPathStep>();
        path = new XmlPath(steps, XmlTerminal.Element, null);
        error = null;
        if (string.IsNullOrWhiteSpace(selector))
        {
            return true;
        }

        if (!QueryLimits.IsSelectorLengthAllowed(selector, out error))
        {
            return false;
        }

        var text = selector.Trim();
        if (text[0] != '/')
        {
            error = "An XML selector must start with '/', "
                + "for example /Project/PropertyGroup or //PackageReference.";
            return false;
        }

        if (text == "/")
        {
            return true;
        }

        var segments = text.Split('/');
        var terminal = XmlTerminal.Element;
        string? terminalName = null;
        var pendingDescendant = false;
        for (var index = 1; index < segments.Length; index++)
        {
            var segment = segments[index];
            var isLast = index == segments.Length - 1;
            if (segment.Length == 0)
            {
                if (isLast)
                {
                    error = pendingDescendant
                        ? "The selector ends with '//'."
                        : "The selector ends with '/'.";
                    return false;
                }

                pendingDescendant = true;
                continue;
            }

            if (segment[0] == '@')
            {
                if (!isLast)
                {
                    error = "An attribute step must be the last step of the selector.";
                    return false;
                }

                if (steps.Count == 0)
                {
                    error = "An attribute step must follow at least one element step.";
                    return false;
                }

                var attributeName = segment[1..];
                if (!IsValidName(attributeName))
                {
                    error = $"'{segment}' is not a valid attribute step; "
                        + "use @ followed by the attribute name.";
                    return false;
                }

                terminal = XmlTerminal.Attribute;
                terminalName = attributeName;
                continue;
            }

            if (string.Equals(segment, "text()", StringComparison.Ordinal))
            {
                if (!isLast)
                {
                    error = "A text() step must be the last step of the selector.";
                    return false;
                }

                if (steps.Count == 0)
                {
                    error = "A text() step must follow at least one element step.";
                    return false;
                }

                terminal = XmlTerminal.Text;
                continue;
            }

            var bracket = segment.IndexOf('[');
            string name;
            int? position = null;
            if (bracket >= 0)
            {
                if (!segment.EndsWith(']'))
                {
                    error = $"'{segment}' is not a valid step; use name[n] for the nth match.";
                    return false;
                }

                name = segment[..bracket];
                var inside = segment[(bracket + 1)..^1];
                if (inside.Length == 0 || !inside.All(char.IsAsciiDigit))
                {
                    error = $"'{segment}' is not supported; "
                        + "the only bracket form is name[n] for the nth match.";
                    return false;
                }

                if (!int.TryParse(inside, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                    || parsed < 1)
                {
                    error = $"'{segment}' index must be a 1-based positive integer.";
                    return false;
                }

                position = parsed;
            }
            else
            {
                name = segment;
            }

            if (!IsValidName(name))
            {
                error = $"'{segment}' is not a valid element step; "
                    + "use an element name, '*', name[n], @name, or text().";
                return false;
            }

            steps.Add(new XmlPathStep(pendingDescendant, name, position));
            pendingDescendant = false;
        }

        if (pendingDescendant)
        {
            error = "The selector ends with '//'.";
            return false;
        }

        path = new XmlPath(steps, terminal, terminalName);
        return true;
    }

    private static bool IsValidName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        var wildcard = string.Equals(name, "*", StringComparison.Ordinal);
        foreach (var character in name)
        {
            if (character == '*')
            {
                if (!wildcard)
                {
                    return false;
                }

                continue;
            }

            if (!char.IsLetterOrDigit(character) && character is not ('_' or '-' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }
}
