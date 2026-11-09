using Beesum.Core.Models;
using Beesum.Core.Storage;
using Xunit;

namespace Beesum.Core.Tests.Storage;

public class FileTranscriptCacheTests
{
    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Transcript Sample(string id = "abc", string lang = "en") =>
        new(id, lang, new[]
        {
            new SubtitleSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Hello"),
            new SubtitleSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), "World"),
        });

    [Fact]
    public async Task SetThenGet_RoundTrip()
    {
        var dir = NewDir();
        try
        {
            var cache = new FileTranscriptCache(dir);
            await cache.SetAsync(Sample());

            var loaded = await cache.GetAsync("abc", "en");

            Assert.NotNull(loaded);
            Assert.Equal("abc", loaded.SourceId);
            Assert.Equal("en", loaded.Language);
            Assert.Equal(2, loaded.Segments.Count);
            Assert.Equal("Hello", loaded.Segments[0].Text);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task GetMissing_ReturnsNull()
    {
        var dir = NewDir();
        try
        {
            Assert.Null(await new FileTranscriptCache(dir).GetAsync("absent", "en"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Remove_DeletesEntry()
    {
        var dir = NewDir();
        try
        {
            var cache = new FileTranscriptCache(dir);
            await cache.SetAsync(Sample());
            await cache.RemoveAsync("abc", "en");
            Assert.Null(await cache.GetAsync("abc", "en"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SanitizesSourceId_DoesNotEscapeDirectory()
    {
        var dir = NewDir();
        try
        {
            var cache = new FileTranscriptCache(dir);
            await cache.SetAsync(Sample(id: "../evil", lang: "en"));

            var files = Directory.GetFiles(dir);
            var single = Assert.Single(files);
            Assert.StartsWith(dir, single, StringComparison.OrdinalIgnoreCase);

            var loaded = await cache.GetAsync("../evil", "en");
            Assert.NotNull(loaded);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task CorruptFile_ReturnsNull()
    {
        var dir = NewDir();
        try
        {
            var cache = new FileTranscriptCache(dir);
            await cache.SetAsync(Sample());
            foreach (var file in Directory.GetFiles(dir))
                await File.WriteAllTextAsync(file, "not json");

            Assert.Null(await cache.GetAsync("abc", "en"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
