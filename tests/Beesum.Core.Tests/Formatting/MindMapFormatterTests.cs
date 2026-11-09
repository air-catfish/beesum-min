using Beesum.Core.Formatting;
using Xunit;

namespace Beesum.Core.Tests.Formatting;

public class MindMapFormatterTests
{
    [Fact]
    public void Normalize_ConvertsNestedBulletsToMermaid()
    {
        var result = MindMapFormatter.Normalize("- Caching\n  - Keys\n    - tenant\n  - Invalidation\n");

        Assert.Contains("```mermaid", result, StringComparison.Ordinal);
        Assert.Contains("root((Caching))", result, StringComparison.Ordinal);
        Assert.Contains("\n    Keys", result, StringComparison.Ordinal);
        Assert.Contains("\n      tenant", result, StringComparison.Ordinal);
        Assert.Contains("\n    Invalidation", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_FlatTextBecomesChildren()
    {
        var result = MindMapFormatter.Normalize("one\ntwo\n");

        Assert.Contains("root((Summary))", result, StringComparison.Ordinal);
        Assert.Contains("\n    one", result, StringComparison.Ordinal);
        Assert.Contains("\n    two", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_AlreadyMermaid_IsUntouched()
    {
        const string mermaid = "```mermaid\nmindmap\n  root((x))\n```";

        Assert.Equal(mermaid, MindMapFormatter.Normalize(mermaid));
    }
}
