namespace CrystalCode.Tools.Query;

/// <summary>
/// A query failure whose message is shown to the model.
/// </summary>
internal sealed class QueryException : Exception
{
    public QueryException(string message)
        : base(message)
    {
    }
}
