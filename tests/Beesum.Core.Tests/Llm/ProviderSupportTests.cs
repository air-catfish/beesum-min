using Beesum.Core.Llm;
using Xunit;

namespace Beesum.Core.Tests.Llm;

public class ProviderSupportTests
{
    [Fact]
    public async Task ResolveKeyAsync_ReturnsTheKey()
    {
        var lookup = await ProviderSupport.ResolveKeyAsync(
            _ => Task.FromResult<string?>("sk-test"),
            CancellationToken.None,
            CancellationToken.None);

        Assert.Equal("sk-test", lookup.Key);
        Assert.Null(lookup.Error);
    }

    [Fact]
    public async Task ResolveKeyAsync_Timeout_ReturnsAnError()
    {
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        var lookup = await ProviderSupport.ResolveKeyAsync(
            token => Task.FromCanceled<string?>(token),
            timeout.Token,
            CancellationToken.None);

        Assert.Null(lookup.Key);
        Assert.Contains("Timed out", lookup.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveKeyAsync_CallerCancellation_Propagates()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProviderSupport.ResolveKeyAsync(
                token => Task.FromCanceled<string?>(token),
                caller.Token,
                caller.Token));
    }

    [Fact]
    public async Task ResolveKeyAsync_ResolverThrows_ReturnsAnError()
    {
        var lookup = await ProviderSupport.ResolveKeyAsync(
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None,
            CancellationToken.None);

        Assert.Null(lookup.Key);
        Assert.Contains("boom", lookup.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_LeavesShortTextAlone_AndCutsLongText()
    {
        Assert.Equal("short", ProviderSupport.Truncate("short", 10));
        Assert.Equal("abcde...", ProviderSupport.Truncate("abcdefghij", 5));
    }
}
