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

    /// <summary>
    /// Rejects a selector longer than <see cref="MaximumSelectorCharacters"/>.
    /// </summary>
    /// <param name="selector">The model-supplied selector; null or empty is allowed.</param>
    /// <param name="error">The rejection message when the selector is too long.</param>
    public static bool IsSelectorLengthAllowed(string? selector, out string? error)
    {
        error = null;
        if (selector is not null && selector.Length > MaximumSelectorCharacters)
        {
            error = "The selector is too long.";
            return false;
        }

        return true;
    }
}
