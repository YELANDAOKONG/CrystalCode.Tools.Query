using System.Text;

namespace CrystalCode.Tools.Query.Reading;

/// <summary>
/// Reads one bounded text file: size cap, binary probe, and BOM decode.
/// </summary>
internal static class QueryFileReader
{
    public static async Task<(string? Text, string? Error)> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return (null, "The path is not valid.");
        }

        if (Directory.Exists(path))
        {
            return (null, "The path is a directory, not a file.");
        }

        if (!info.Exists)
        {
            return (null, "The file was not found.");
        }

        if (info.Length > QueryLimits.MaximumFileBytes)
        {
            return (
                null,
                $"The file is larger than {QueryLimits.MaximumFileBytes} bytes; "
                + "use grep, search, or bash to narrow it first.");
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "The file could not be read.");
        }

        var probe = Math.Min(bytes.Length, QueryLimits.BinaryProbeBytes);
        for (var index = 0; index < probe; index++)
        {
            if (bytes[index] == 0)
            {
                return (null, "The file looks binary and is not queried.");
            }
        }

        if (bytes.Length == 0)
        {
            return (null, "The file is empty.");
        }

        return (Decode(bytes), null);
    }

    private static string Decode(byte[] bytes)
    {
        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
        {
            return Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4);
        }

        if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
        {
            return new UTF32Encoding(bigEndian: true, byteOrderMark: false)
                .GetString(bytes, 4, bytes.Length - 4);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool HasPrefix(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }
}
