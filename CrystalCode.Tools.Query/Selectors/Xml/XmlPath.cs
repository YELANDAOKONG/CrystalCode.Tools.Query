namespace CrystalCode.Tools.Query.Selectors.Xml;

/// <summary>
/// A parsed XML selector: element steps and the value the path selects.
/// </summary>
internal sealed record XmlPath(
    IReadOnlyList<XmlPathStep> Steps,
    XmlTerminal Terminal,
    string? TerminalName);
