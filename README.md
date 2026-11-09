# beesum-min

![beesum-min](docs/logo.png)

**Transcript-first YouTube summarizer for the terminal: yt-dlp + WebVTT + local or API LLMs.**

[![CI](https://github.com/air-catfish/beesum-min/actions/workflows/ci.yml/badge.svg)](https://github.com/air-catfish/beesum-min/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Downloads YouTube subtitles with yt-dlp, parses WebVTT, and prints a clean
transcript or a summary. `transcript-only` (default) never sends the transcript
anywhere; Ollama stays local; API providers are opt-in.

<img src="docs/demos/real-video.gif" width="720" alt="beesum-min on a 3blue1brown video">

## Status

No NuGet packages yet. The source builds and is tested; packages are prepared with
`dotnet pack` and will be published with the first release.

## Build and test

```powershell
dotnet build beesum-min.slnx -c Release
dotnet run --project tests\Beesum.Core.Tests
dotnet run --project tests\Beesum.Min.Cli.Tests
dotnet run --project tests\Beesum.Testing.Tests
dotnet run --project tests\Beesum.Mcp.Tests
```

## Use

```powershell
dotnet run --project src\Beesum.Min.Cli -- --local-vtt samples\demo\talk.vtt
dotnet run --project src\Beesum.Min.Cli -- "https://www.youtube.com/watch?v=..." --json
```

`--prompt-file`, `--instructions`, `--sub-langs`, `--timestamps`, `--format mindmap`,
`--chunk-tokens`, `--stdin`, `--batch`, `--proxy` — see `--help`.

## Docs

- [Embedding](docs/embedding.md) · [Prompts](docs/prompts.md) · [MCP](docs/mcp.md)
- [Languages](docs/languages.md) · [Chunking](docs/chunking.md) · [For agents](docs/for-agents.md)

## License

MIT. Report security issues through GitHub advisories.
