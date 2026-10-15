namespace CrystalCode.Tools.Query;

/// <summary>
/// Model-facing and file limits for the query tool.
/// </summary>
internal static class QueryLimits
{
    public const int MaximumFileBytes = 16 * 1024 * 1024;
    public const int BinaryProbeBytes = 8192;
    public const int DefaultPageItems = 50;
    public const int MaximumPageItems = 200;
    public const int MaximumResultCharacters = 50_000;
    public const int MaximumScalarCharacters = 20_000;
    public const int MaximumSelectorCharacters = 2048;
    public const int MaximumPreviewMembers = 50;
    public const int MaximumJsonDepth = 512;
}
