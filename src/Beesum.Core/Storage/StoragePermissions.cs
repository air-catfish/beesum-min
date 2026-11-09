namespace Beesum.Core.Storage;

/// <summary>
/// Best-effort private permissions for per-user files. Windows inherits the
/// profile ACL; Unix gets 0600/0700 so other local users cannot read transcripts
/// or API keys.
/// </summary>
internal static class StoragePermissions
{
    public static void PrivateFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        TrySetFile(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static void PrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        TrySetDirectory(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void TrySetFile(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, mode); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
    }

    private static void TrySetDirectory(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows()) return;
        try { Directory.CreateDirectory(path); File.SetUnixFileMode(path, mode); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
    }
}
