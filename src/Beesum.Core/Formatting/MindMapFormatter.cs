using System.Text.RegularExpressions;

namespace Beesum.Core.Formatting;

/// <summary>
/// Converts a Markdown outline from an LLM into a valid Mermaid mindmap.
/// </summary>
public static partial class MindMapFormatter
{
    /// <summary>
    /// Returns a fenced Mermaid mindmap. Already-Mermaid input is left untouched;
    /// a flat answer becomes a root with one child per non-empty line.
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text)) return text;

        var trimmed = text.Trim();
        if (trimmed.Contains("```mermaid", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var items = ParseItems(text);
        if (items.Count == 0)
        {
            var fallback = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(12)
                .Select(line => (Depth: 1, Text: line));

            return Build("Summary", fallback);
        }

        var first = items[0];
        var rest = items.Skip(1).Select(item => (Depth: item.Depth, Text: item.Text));

        return first.Depth == 0
            ? Build(first.Text, rest)
            : Build("Summary", items.Select(item => (Depth: item.Depth + 1, Text: item.Text)));
    }

    private static List<(int Depth, string Text)> ParseItems(string text)
    {
        var items = new List<(int Depth, string Text)>();
        var indents = new List<int>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var match = Bullet().Match(line);
            var heading = Heading().Match(line);
            if (!match.Success && !heading.Success) continue;

            var indent = match.Success
                ? match.Groups["indent"].Value.Select(c => c == '\t' ? 4 : 1).Sum()
                : heading.Groups["hashes"].Value.Length - 1;
            var label = match.Success ? match.Groups["text"].Value : heading.Groups["text"].Value;

            while (indents.Count > 0 && indent <= indents[^1]) indents.RemoveAt(indents.Count - 1);
            indents.Add(indent);

            items.Add((indents.Count - 1, Clean(label)));
        }

        return items;
    }

    private static string Build(string root, IEnumerable<(int Depth, string Text)> items)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("```mermaid");
        builder.AppendLine("mindmap");
        builder.Append("  root((").Append(Clean(root)).AppendLine("))");

        var lastDepth = 0;
        foreach (var item in items)
        {
            var depth = Math.Clamp(item.Depth, 1, lastDepth + 1);
            if (string.IsNullOrWhiteSpace(item.Text)) continue;

            builder.Append(' ', 2 + depth * 2).AppendLine(item.Text);
            lastDepth = depth;
        }

        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    private static string Clean(string value)
    {
        var cleaned = value
            .Replace("**", string.Empty)
            .Replace("`", string.Empty)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Replace("{", string.Empty)
            .Replace("}", string.Empty)
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace("\"", string.Empty);

        return Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', '.', ':', ';');
    }

    [GeneratedRegex(@"^(?<indent>[ \t]*)[-*+]\s+(?<text>.+)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<text>.+)$")]
    private static partial Regex Heading();
}
