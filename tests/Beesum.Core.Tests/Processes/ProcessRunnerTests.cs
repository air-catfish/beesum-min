using Beesum.Core.Processes;
using Xunit;

namespace Beesum.Core.Tests.Processes;

public class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_DotnetVersion_ReturnsSuccessWithStdout()
    {
        var result = await _runner.RunAsync(
            "dotnet",
            new[] { "--version" },
            cancellationToken: CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    [Fact]
    public async Task RunAsync_UnknownDotnetCommand_ReturnsNonZeroExitCode()
    {
        var result = await _runner.RunAsync(
            "dotnet",
            new[] { "nonexistent-command-xyz-123" },
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_Cancellation_KillsProcessAndThrows()
    {
        var (fileName, args) = OperatingSystem.IsWindows()
            ? ("ping", new[] { "-n", "30", "127.0.0.1" })
            : ("sleep", new[] { "30" });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _runner.RunAsync(fileName, args, cancellationToken: cts.Token));

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Cancellation took {stopwatch.Elapsed}; expected the process to be killed promptly.");
    }
}
