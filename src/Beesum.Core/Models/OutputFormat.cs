namespace Beesum.Core.Models;

/// <summary>Format of the generated summary.</summary>
public enum OutputFormat
{
    /// <summary>Markdown with headings and lists.</summary>
    Markdown,
    /// <summary>Plain text without markup.</summary>
    PlainText,
    /// <summary>A Mermaid mindmap for an LLM to fill in.</summary>
    MindMap
}
