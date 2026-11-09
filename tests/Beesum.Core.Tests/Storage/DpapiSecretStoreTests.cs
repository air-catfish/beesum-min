using Beesum.Core.Storage;
using Xunit;

namespace Beesum.Core.Tests.Storage;

public class DpapiSecretStoreTests
{
    private static string NewPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-secrets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "secrets.dat");
    }

    [Fact]
    public async Task SetThenGet_RoundTrip()
    {
        var path = NewPath();
        try
        {
            var store = new DpapiSecretStore(path);
            await store.SetAsync("openai.key", "sk-test-123");

            Assert.Equal("sk-test-123", await store.GetAsync("openai.key"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task GetMissing_ReturnsNull()
    {
        var path = NewPath();
        try
        {
            Assert.Null(await new DpapiSecretStore(path).GetAsync("absent"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task Remove_DeletesSecret()
    {
        var path = NewPath();
        try
        {
            var store = new DpapiSecretStore(path);
            await store.SetAsync("k", "v");
            await store.RemoveAsync("k");
            Assert.Null(await store.GetAsync("k"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var path = NewPath();
        try
        {
            await new DpapiSecretStore(path).SetAsync("k", "v");
            var second = new DpapiSecretStore(path);
            Assert.Equal("v", await second.GetAsync("k"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task CorruptFile_ReturnsEmpty()
    {
        var path = NewPath();
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            var store = new DpapiSecretStore(path);
            Assert.Null(await store.GetAsync("k"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task OnUnix_FileHasRestrictivePermissions()
    {
        if (OperatingSystem.IsWindows()) return; // DPAPI is the protection on Windows

        var path = NewPath();
        try
        {
            await new DpapiSecretStore(path).SetAsync("k", "v");

            var mode = File.GetUnixFileMode(path);
            Assert.False(mode.HasFlag(UnixFileMode.OtherRead));
            Assert.False(mode.HasFlag(UnixFileMode.GroupRead));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
