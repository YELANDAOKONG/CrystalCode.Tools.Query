namespace CrystalCode.Tools.Query.Xml;

/// <summary>
/// One step of a parsed XML path.
/// </summary>
/// <param name="Descendant">Whether the step matches at any depth.</param>
/// <param name="Name">The element local name, or * for any element.</param>
/// <param name="Index">The optional 1-based nth-match position.</param>
internal sealed record XmlPathStep(bool Descendant, string Name, int? Index);
