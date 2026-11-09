using Beesum.Core.Abstractions;
using Beesum.Core.Models;

namespace Beesum.Testing;

/// <summary>
/// <see cref="IProcessRunner"/> test double: returns queued results/exceptions
/// and records every invocation for assertions.
/// </summary>
public sealed class InMemoryProcessRunner : IProcessRunner
{
    private readonly Queue<object> _responses = new();

    /// <summary>All invocations, in order, that this runner has received.</summary>
    public List<Invocation> Invocations { get; } = new();

    /// <summary>A single recorded invocation.</summary>
    /// <param name="FileName">Executable name.</param>
    /// <param name="Arguments">Argument list.</param>
    /// <param name="WorkingDirectory">Working directory, if any.</param>
    public sealed record Invocation(
        string FileName,
        IReadOnlyList<string> Arguments,
        string? WorkingDirectory);

    /// <summary>Queues a result to return on the next call.</summary>
    /// <param name="result">Result to return.</param>
    public InMemoryProcessRunner EnqueueResult(ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _responses.Enqueue(result);
        return this;
    }

    /// <summary>Queues a result to return on the next call.</summary>
    /// <param name="exitCode">Process exit code.</param>
    /// <param name="stdout">Captured stdout.</param>
    /// <param name="stderr">Captured stderr.</param>
    public InMemoryProcessRunner EnqueueResult(int exitCode, string stdout = "", string stderr = "")
        => EnqueueResult(new ProcessResult(exitCode, stdout, stderr));

    /// <summary>Queues an exception to throw on the next call.</summary>
    /// <param name="exception">Exception to throw.</param>
    public InMemoryProcessRunner EnqueueException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _responses.Enqueue(exception);
        return this;
    }

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Invocations.Add(new Invocation(fileName, arguments, workingDirectory));

        if (_responses.Count == 0)
            throw new InvalidOperationException(
                $"InMemoryProcessRunner has no queued response for call #{Invocations.Count} ('{fileName}').");

        return _responses.Dequeue() switch
        {
            ProcessResult result => Task.FromResult(result),
            Exception exception => Task.FromException<ProcessResult>(exception),
            var other => throw new InvalidOperationException(
                $"Unexpected queued response type: {other.GetType()}."),
        };
    }
}
