using Beesum.Core.Storage;
using Beesum.Core.YtDlp;
using Xunit;

namespace Beesum.Core.Tests.YtDlp;

public class YtDlpPathResolverTests
{
    private static string ExeName => OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "beesum-resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CreateExecutable(string path)
    {
        File.WriteAllText(path, string.Empty);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static YtDlpPathResolver NewResolver(
        FileSettingsStore settings,
        string managedDir,
        string? searchPath = null) =>
        new(settings, managedDir, searchPath: searchPath);

    [Fact]
    public async Task ResolveAsync_ExplicitSettingPresent_ReturnsExplicit()
    {
        var root = NewDir();
        try
        {
            var managed = Path.Combine(root, "tools");
            Directory.CreateDirectory(managed);

            var explicitPath = Path.Combine(root, "custom-yt-dlp");
            CreateExecutable(explicitPath);
            var settings = new FileSettingsStore(Path.Combine(root, "settings.json"));
            await settings.SetAsync(YtDlpPathResolver.SettingKey, explicitPath);

            var result = await NewResolver(settings, managed, searchPath: string.Empty).ResolveAsync();

            Assert.NotNull(result);
            Assert.Equal(explicitPath, result.Path);
            Assert.Equal(YtDlpSource.Explicit, result.Source);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ResolveAsync_ManagedPresent_ReturnsManaged()
    {
        var root = NewDir();
        try
        {
            var managed = Path.Combine(root, "tools");
            Directory.CreateDirectory(managed);
            var managedExe = Path.Combine(managed, ExeName);
            CreateExecutable(managedExe);

            var settings = new FileSettingsStore(Path.Combine(root, "settings.json"));
            var result = await NewResolver(settings, managed, searchPath: string.Empty).ResolveAsync();

            Assert.NotNull(result);
            Assert.Equal(managedExe, result.Path);
            Assert.Equal(YtDlpSource.Managed, result.Source);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ResolveAsync_OnlyInPath_ReturnsPath()
    {
        var root = NewDir();
        try
        {
            var managed = Path.Combine(root, "tools");
            Directory.CreateDirectory(managed);

            var bin = Path.Combine(root, "bin");
            Directory.CreateDirectory(bin);
            var exe = Path.Combine(bin, ExeName);
            CreateExecutable(exe);

            var settings = new FileSettingsStore(Path.Combine(root, "settings.json"));
            var result = await NewResolver(settings, managed, searchPath: bin).ResolveAsync();

            Assert.NotNull(result);
            Assert.Equal(exe, result.Path);
            Assert.Equal(YtDlpSource.Path, result.Source);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ResolveAsync_NotFoundAnywhere_ReturnsNull()
    {
        var root = NewDir();
        try
        {
            var managed = Path.Combine(root, "tools");
            Directory.CreateDirectory(managed);

            var settings = new FileSettingsStore(Path.Combine(root, "settings.json"));
            var result = await NewResolver(settings, managed, searchPath: root).ResolveAsync();

            Assert.Null(result);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ResolveAsync_ExplicitSettingMissing_FallsThroughToManaged()
    {
        var root = NewDir();
        try
        {
            var managed = Path.Combine(root, "tools");
            Directory.CreateDirectory(managed);
            var managedExe = Path.Combine(managed, ExeName);
            CreateExecutable(managedExe);

            var settings = new FileSettingsStore(Path.Combine(root, "settings.json"));
            await settings.SetAsync(YtDlpPathResolver.SettingKey, "/nonexistent/yt-dlp");

            var result = await NewResolver(settings, managed, searchPath: string.Empty).ResolveAsync();

            Assert.NotNull(result);
            Assert.Equal(managedExe, result.Path);
            Assert.Equal(YtDlpSource.Managed, result.Source);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FindOnPath_FirstEntryWins()
    {
        var root = NewDir();
        try
        {
            var first = Path.Combine(root, "first");
            var second = Path.Combine(root, "second");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            var firstExe = Path.Combine(first, ExeName);
            CreateExecutable(firstExe);
            CreateExecutable(Path.Combine(second, ExeName));

            var path = string.Join(Path.PathSeparator, first, second);

            Assert.Equal(firstExe, YtDlpPathResolver.FindOnPath(ExeName, path));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FindOnPath_HandlesQuotedEntries()
    {
        var root = NewDir();
        try
        {
            var bin = Path.Combine(root, "bin with space");
            Directory.CreateDirectory(bin);
            var exe = Path.Combine(bin, ExeName);
            CreateExecutable(exe);

            Assert.Equal(exe, YtDlpPathResolver.FindOnPath(ExeName, $"\"{bin}\""));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FindOnPath_IgnoresNonExecutableFileOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;

        var root = NewDir();
        try
        {
            var bin = Path.Combine(root, "bin");
            Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(bin, ExeName), string.Empty); // no execute bit

            Assert.Null(YtDlpPathResolver.FindOnPath(ExeName, bin));
        }
        finally { Directory.Delete(root, true); }
    }
}
