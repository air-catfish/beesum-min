using Beesum.Core.Storage;
using Xunit;

namespace Beesum.Core.Tests.Storage;

public class FileSettingsStoreTests
{
    private static string NewPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    [Fact]
    public async Task SetThenGet_ReturnsValue()
    {
        var path = NewPath();
        try
        {
            var store = new FileSettingsStore(path);
            await store.SetAsync("theme", "dark");
            await store.SetAsync("count", 42);

            Assert.Equal("dark", await store.GetAsync<string>("theme"));
            Assert.Equal(42, await store.GetAsync<int>("count"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task GetMissing_ReturnsDefault()
    {
        var path = NewPath();
        try
        {
            var store = new FileSettingsStore(path);
            Assert.Null(await store.GetAsync<string>("absent"));
            Assert.Null(await store.GetAsync<int?>("absent"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task Remove_DeletesValue()
    {
        var path = NewPath();
        try
        {
            var store = new FileSettingsStore(path);
            await store.SetAsync("k", "v");
            await store.RemoveAsync("k");

            Assert.Null(await store.GetAsync<string>("k"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task CorruptFile_ReturnsDefault()
    {
        var path = NewPath();
        try
        {
            await File.WriteAllTextAsync(path, "not json at all");
            var store = new FileSettingsStore(path);
            Assert.Null(await store.GetAsync<string>("k"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var path = NewPath();
        try
        {
            await new FileSettingsStore(path).SetAsync("k", "v");
            var second = new FileSettingsStore(path);
            Assert.Equal("v", await second.GetAsync<string>("k"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
