using System.Xml.Linq;

namespace CrystalCode.Tools.Query.Selectors.Xml;

/// <summary>
/// The result of one XML selector: matched elements or extracted values.
/// </summary>
internal sealed class XmlSelection
{
    private XmlSelection(
        XmlSelectionKind kind,
        IReadOnlyList<XElement> elements,
        IReadOnlyList<string> values)
    {
        Kind = kind;
        Elements = elements;
        Values = values;
    }

    public XmlSelectionKind Kind { get; }

    public IReadOnlyList<XElement> Elements { get; }

    public IReadOnlyList<string> Values { get; }

    public static XmlSelection ForElements(IReadOnlyList<XElement> elements) =>
        new(XmlSelectionKind.Elements, elements, []);

    public static XmlSelection ForValues(IReadOnlyList<string> values) =>
        new(XmlSelectionKind.Values, [], values);
}
