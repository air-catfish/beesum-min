namespace Beesum.Min.Cli;

/// <summary>
/// Asks an interactive terminal where to save the transcript and the raw VTT.
/// Piped, JSON, batch, and stdin runs never prompt, so scripts and agents keep working.
/// </summary>
internal static class CliSavePrompts
{
    public static CliOptions Apply(CliOptions options)
    {
        if (options.NoSavePrompt ||
            options.Json ||
            options.BatchFile is not null ||
            options.Stdin ||
            options.ShowHelp ||
            options.ShowVersion ||
            Console.IsInputRedirected)
        {
            return options;
        }

        var transcript = options.SaveTranscriptPath ?? Ask("Save transcript?", "transcript.md");
        var vtt = options.SaveVttPath ?? Ask("Save subtitles (VTT)?", "captions.vtt");

        return options with { SaveTranscriptPath = transcript, SaveVttPath = vtt };
    }

    private static string? Ask(string question, string defaultPath)
    {
        Console.Error.Write($"{question} [y/N] ");
        var answer = Console.ReadLine();
        if (!IsYes(answer)) return null;

        Console.Error.Write($"Path [{defaultPath}]: ");
        var path = Console.ReadLine();
        return string.IsNullOrWhiteSpace(path) ? defaultPath : path.Trim();
    }

    private static bool IsYes(string? value)
    {
        var answer = value?.Trim();
        return answer is not null &&
               (answer.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("д", StringComparison.OrdinalIgnoreCase) ||
                answer.Equals("да", StringComparison.OrdinalIgnoreCase));
    }
}
