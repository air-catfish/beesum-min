using Beesum.Core.Models;

namespace Beesum.Core.Abstractions;

/// <summary>
/// Abstraction over <see cref="System.Diagnostics.Process"/> so callers (and tests)
/// can run external tools without depending on the real process APIs.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with the given argument list.
    /// Arguments are always passed as a list to avoid shell-quoting bugs.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}
