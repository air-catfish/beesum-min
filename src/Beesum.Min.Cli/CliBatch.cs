using System.Text;
using Beesum.Core.Models;

namespace Beesum.Min.Cli;

internal sealed record BatchItem(string Url, SummaryResult Result);

internal static class CliBatch
{
    /// <summary>Reads URLs: one per line; blanks and lines starting with # are skipped.</summary>
    public static IReadOnlyList<string> ReadUrls(string path)
    {
        if (!File.Exists(path))
            throw new ArgumentException($"Batch file not found: {path}");

        try
        {
            return File.ReadLines(path)
                .Select(line => line.Trim().TrimStart('\uFEFF'))
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException($"Cannot read batch file: {ex.Message}");
        }
    }

    public static string FormatText(IReadOnlyList<BatchItem> items)
    {
        var builder = new StringBuilder();
        foreach (var item in items)
        {
            if (builder.Length > 0) builder.AppendLine();

            builder.Append("# ").AppendLine(item.Url);
            builder.AppendLine(item.Result.IsSuccess
                ? item.Result.Content
                : item.Result.ErrorMessage ?? "Unknown error");
        }

        return builder.ToString();
    }
}
