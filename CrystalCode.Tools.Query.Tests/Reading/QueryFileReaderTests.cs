using System.Text;

using CrystalCode.Tools.Query.Reading;

namespace CrystalCode.Tools.Query.Tests.Reading;

public sealed class QueryFileReaderTests
{
    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    [InlineData("utf-32BE")]
    public async Task ReadAsync_BomEncodedText_ReturnsDecodedText(string encodingName)
    {
        using var temp = new TemporaryDirectory();
        var encoding = Encoding.GetEncoding(encodingName);
        const string Text = "{\"name\":\"hello\"}";
        var path = temp.WriteBytes("data.json", [.. encoding.GetPreamble(), .. encoding.GetBytes(Text)]);

        var (text, error) = await QueryFileReader.ReadAsync(path, CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(Text, text);
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-32")]
    public async Task ReadAsync_EncodedNullCharacter_RejectsBinary(string encodingName)
    {
        using var temp = new TemporaryDirectory();
        var encoding = Encoding.GetEncoding(encodingName);
        var path = temp.WriteBytes("data.json", [.. encoding.GetPreamble(), .. encoding.GetBytes("a\0b")]);

        var (text, error) = await QueryFileReader.ReadAsync(path, CancellationToken.None);

        Assert.Null(text);
        Assert.Contains("looks binary", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8192)]
    public async Task ReadBoundedAsync_AtOrAboveLimit_EnforcesReadBoundary(int extraBytes)
    {
        using var stream = new MemoryStream(new byte[QueryLimits.MaximumFileBytes + extraBytes]);

        var bytes = await QueryFileReader.ReadBoundedAsync(stream, CancellationToken.None);

        if (extraBytes == 0)
        {
            Assert.NotNull(bytes);
            Assert.Equal(QueryLimits.MaximumFileBytes, bytes.Length);
        }
        else
        {
            Assert.Null(bytes);
            Assert.Equal(QueryLimits.MaximumFileBytes + 1, stream.Position);
        }
    }

    [Fact]
    public async Task ReadBoundedAsync_Cancelled_ThrowsCancellation()
    {
        using var stream = new MemoryStream([1]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => QueryFileReader.ReadBoundedAsync(stream, cancellation.Token));
    }
}
