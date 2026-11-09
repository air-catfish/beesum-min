using Beesum.Core.Models;
using Beesum.Core.Storage;
using Xunit;

namespace Beesum.Core.Tests.Storage;

public class JsonSummaryHistoryTests
{
    private static string NewPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "history.jsonl");
    }

    private static SummaryHistoryEntry Entry(string id, DateTimeOffset? createdAt = null) =>
        new(
            id,
            "src-" + id,
            "Title " + id,
            createdAt ?? DateTimeOffset.UtcNow,
            new SummaryResult(true, "content " + id, "transcript-only"));

    [Fact]
    public async Task AddThenGetAll_ReturnsNewestFirst()
    {
        var path = NewPath();
        try
        {
            var history = new JsonSummaryHistory(path);
            await history.AddAsync(Entry("a"));
            await history.AddAsync(Entry("b"));
            await history.AddAsync(Entry("c"));

            var all = await history.GetAllAsync();

            Assert.Equal(new[] { "c", "b", "a" }, all.Select(e => e.Id));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task Remove_DeletesById()
    {
        var path = NewPath();
        try
        {
            var history = new JsonSummaryHistory(path);
            await history.AddAsync(Entry("a"));
            await history.AddAsync(Entry("b"));

            await history.RemoveAsync("a");
            var all = await history.GetAllAsync();

            var only = Assert.Single(all);
            Assert.Equal("b", only.Id);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task CorruptLine_IsSkipped()
    {
        var path = NewPath();
        try
        {
            var history = new JsonSummaryHistory(path);
            await history.AddAsync(Entry("a"));
            await File.AppendAllTextAsync(path, "not-json\n");
            await history.AddAsync(Entry("b"));

            var all = await history.GetAllAsync();

            Assert.Equal(new[] { "b", "a" }, all.Select(e => e.Id));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task MissingFile_ReturnsEmpty()
    {
        var path = NewPath();
        try
        {
            Assert.Empty(await new JsonSummaryHistory(path).GetAllAsync());
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var path = NewPath();
        try
        {
            await new JsonSummaryHistory(path).AddAsync(Entry("a"));
            var second = new JsonSummaryHistory(path);
            var all = await second.GetAllAsync();
            Assert.Single(all);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
