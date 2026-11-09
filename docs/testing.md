# How this is tested

No coverage badges, no “100% tested” claims. The suite is small and opinionated.

## What exists

- **265 tests**: Core 192, CLI 53, Testing 8, MCP 12.
- **Provider contract tests**: every `ILlmProvider` must return a stable id, a
  non-null availability, throw on a null request, and produce consistent metadata.
- **Golden tests**: exact prompt strings for all style/format combinations and an
  exact JSON shape for `--json`.
- **Deterministic fuzz**: 500 pseudo-random strings into the VTT parser, plus
  monotonic timestamps and CRLF/LF equivalence.
- **MCP protocol tests**: initialize, tools/list, tools/call, unknown methods, malformed JSON, VTT input.
- **Real boundary tests**: cancellation, timeouts, missing yt-dlp, failed chunks,
  language preference, path traversal in cache keys.

The suite never touches the network. `yt-dlp`, HTTP, and Ollama are replaced with
in-memory doubles from `Beesum.Testing`.

## How it runs

```powershell
dotnet build beesum-min.slnx -c Release
dotnet run --project tests\Beesum.Core.Tests
dotnet run --project tests\Beesum.Min.Cli.Tests
dotnet run --project tests\Beesum.Testing.Tests
dotnet run --project tests\Beesum.Mcp.Tests
```

CI runs the same commands on `windows-latest` and `ubuntu-latest`, with warnings
as errors.

## What is not tested

- Live YouTube downloads and the proxy path: verified manually on a real video
  (3blue1brown, `fNk_zzaMoSs`) through a SOCKS5 proxy.
- Live API calls: the provider tests use fake HTTP; real keys are not part of CI.
- Mutation testing: Stryker.NET is not wired in yet, so there is no mutation score.
- GUI: it lives in a separate private repository.

## Why no mocking library

The doubles are plain classes: `InMemoryProcessRunner`, `InMemoryHttpMessageHandler`,
`InMemoryLlmProvider`, and friends. They record calls and queue responses, which
keeps tests readable and the package free of a mocking dependency.
