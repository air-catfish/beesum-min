using Beesum.Core.Models;
using Beesum.Min.Cli;
using Xunit;

namespace Beesum.Min.Cli.Tests;

public class CliBatchTests
{
    private static string WriteBatch(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "beesum-batch-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadUrls_SkipsBlanksAndComments()
    {
        var path = WriteBatch("# comment\nhttps://a\n\n  https://b  \n");
        try
        {
            var urls = CliBatch.ReadUrls(path);

            Assert.Equal(new[] { "https://a", "https://b" }, urls);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadUrls_MissingFile_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), "beesum-batch-missing-" + Guid.NewGuid().ToString("N") + ".txt");

        Assert.Throws<ArgumentException>(() => CliBatch.ReadUrls(path));
    }

    [Fact]
    public void FormatText_KeepsProviderErrorsReadable()
    {
        var items = new[]
        {
            new BatchItem("https://a", new SummaryResult(true, "Hello", "ollama")),
            new BatchItem("https://b", new SummaryResult(false, string.Empty, null, "ERROR: nope")),
        };

        var text = CliBatch.FormatText(items);

        Assert.Contains("# https://a", text, StringComparison.Ordinal);
        Assert.Contains("Hello", text, StringComparison.Ordinal);
        Assert.Contains("ERROR: nope", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR: ERROR:", text, StringComparison.Ordinal);
    }
    [Fact]
    public void ReadUrls_StripsUtf8Bom()
    {
        var path = WriteBatch("\uFEFFhttps://a\nhttps://b\n");
        try
        {
            var urls = CliBatch.ReadUrls(path);

            Assert.Equal(new[] { "https://a", "https://b" }, urls);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
