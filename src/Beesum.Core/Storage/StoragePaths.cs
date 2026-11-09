namespace Beesum.Core.Storage;

/// <summary>
/// Computes per-user storage locations for settings, secrets, transcripts, and history.
/// </summary>
internal sealed class StoragePaths
{
    /// <summary>Root directory under which all storage files live.</summary>
    public string Root { get; }

    /// <summary>Path to the JSON settings file.</summary>
    public string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>Path to the encrypted secrets blob (or plaintext on non-Windows).</summary>
    public string SecretsFile => Path.Combine(Root, "secrets.dat");

    /// <summary>Path to the JSONL summary history file.</summary>
    public string HistoryFile => Path.Combine(Root, "history.jsonl");

    /// <summary>Directory holding per-transcript cache files.</summary>
    public string TranscriptsDirectory => Path.Combine(Root, "transcripts");

    /// <summary>Creates storage paths rooted at <paramref name="root"/>.</summary>
    public StoragePaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    /// <summary>
    /// Creates storage paths in the platform's user configuration directory:
    /// <c>%APPDATA%/Beesum</c> on Windows, <c>~/.config/Beesum</c> on Linux/macOS.
    /// </summary>
    public static StoragePaths Default()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new StoragePaths(Path.Combine(baseDir, "Beesum"));
    }
}
