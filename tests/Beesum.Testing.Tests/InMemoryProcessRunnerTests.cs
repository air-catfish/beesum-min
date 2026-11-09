using Beesum.Testing;
using Xunit;

namespace Beesum.Testing.Tests;

public class InMemoryProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ReturnsQueuedResult()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(exitCode: 0, stdout: "hello");

        var result = await runner.RunAsync("tool", new[] { "a" }, cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_RecordsInvocation()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(0);

        await runner.RunAsync(
            "tool",
            new[] { "arg1", "arg2" },
            workingDirectory: "/tmp",
            cancellationToken: CancellationToken.None);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("tool", invocation.FileName);
        Assert.Equal(new[] { "arg1", "arg2" }, invocation.Arguments);
        Assert.Equal("/tmp", invocation.WorkingDirectory);
    }

    [Fact]
    public async Task RunAsync_EmptyQueue_Throws()
    {
        var runner = new InMemoryProcessRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("tool", Array.Empty<string>(), cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_QueuedException_Propagates()
    {
        var runner = new InMemoryProcessRunner().EnqueueException(new TimeoutException("boom"));

        await Assert.ThrowsAsync<TimeoutException>(
            () => runner.RunAsync("tool", Array.Empty<string>(), cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelledToken_ThrowsWithoutRecording()
    {
        var runner = new InMemoryProcessRunner().EnqueueResult(0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync("tool", Array.Empty<string>(), cancellationToken: cts.Token));

        Assert.Empty(runner.Invocations);
    }
}
