using Beesum.Core.Llm;
using Beesum.Core.Models;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class LlmPromptsTests
{
    private static SummaryRequest Request(
        string? instructions = null,
        string? systemPrompt = null,
        bool timestamps = false) =>
        new(
            new Transcript("src", "en", Array.Empty<SubtitleSegment>()),
            PromptStyle.Neutral,
            OutputFormat.Markdown,
            instructions,
            systemPrompt,
            timestamps);

    [Theory]
    [InlineData(PromptStyle.Neutral, OutputFormat.PlainText,
        "You are a neutral assistant. Be objective and concise. Return plain text only, without Markdown markup. Summarize the following transcript.")]
    [InlineData(PromptStyle.Neutral, OutputFormat.Markdown,
        "You are a neutral assistant. Be objective and concise. Format the answer as Markdown with headings and bullet lists. Summarize the following transcript.")]
    [InlineData(PromptStyle.Historian, OutputFormat.PlainText,
        "You are a historian. Write in a narrative, contextual tone. Return plain text only, without Markdown markup. Summarize the following transcript.")]
    [InlineData(PromptStyle.Historian, OutputFormat.Markdown,
        "You are a historian. Write in a narrative, contextual tone. Format the answer as Markdown with headings and bullet lists. Summarize the following transcript.")]
    [InlineData(PromptStyle.Programmer, OutputFormat.PlainText,
        "You are a software engineer. Focus on technical details, code, and precise terminology. Return plain text only, without Markdown markup. Summarize the following transcript.")]
    [InlineData(PromptStyle.Programmer, OutputFormat.Markdown,
        "You are a software engineer. Focus on technical details, code, and precise terminology. Format the answer as Markdown with headings and bullet lists. Summarize the following transcript.")]
    public void BuildSystemPrompt_ReturnsExactString(
        PromptStyle style,
        OutputFormat format,
        string expected)
    {
        Assert.Equal(expected, LlmPrompts.BuildSystemPrompt(style, format));
    }
    [Fact]
    public void DefaultPrompt_ContainsStyleAndFormat()
    {
        var prompt = LlmPrompts.BuildSystemPrompt(Request());

        Assert.Contains("neutral assistant", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Markdown", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Instructions_AreAppendedToTheDefaultPrompt()
    {
        var prompt = LlmPrompts.BuildSystemPrompt(Request("Five bullets, no fluff."));

        Assert.Contains("neutral assistant", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("Five bullets, no fluff.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemPrompt_ReplacesStyleAndFormat()
    {
        var prompt = LlmPrompts.BuildSystemPrompt(Request("ignored", "You are terse."));

        Assert.Equal("You are terse.", prompt);
        Assert.DoesNotContain("neutral assistant", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Timestamps_AreRequestedInThePrompt()
    {
        var prompt = LlmPrompts.BuildSystemPrompt(Request(timestamps: true));

        Assert.Contains("[hh:mm:ss]", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void MindMap_AsksForMermaid()
    {
        var request = new SummaryRequest(
            new Transcript("src", "en", Array.Empty<SubtitleSegment>()),
            PromptStyle.Neutral,
            OutputFormat.MindMap);

        var prompt = LlmPrompts.BuildSystemPrompt(request);

        Assert.Contains("nested bullet list", prompt, StringComparison.Ordinal);
        Assert.Contains("mindmap", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlankInstructions_DoNotChangeThePrompt()
    {
        var without = LlmPrompts.BuildSystemPrompt(Request());
        var blank = LlmPrompts.BuildSystemPrompt(Request("   "));

        Assert.Equal(without, blank);
    }
}
