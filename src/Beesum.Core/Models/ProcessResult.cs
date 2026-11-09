namespace Beesum.Core.Models;

/// <summary>Result of running an external process to completion.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured stdout.</param>
/// <param name="StandardError">Captured stderr.</param>
public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
