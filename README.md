# beesum-min

![beesum-min](docs/logo.png)

**Transcript-first YouTube summarizer for the terminal: yt-dlp + WebVTT + local or API LLMs.**

[![CI](https://github.com/air-catfish/beesum-min/actions/workflows/ci.yml/badge.svg)](https://github.com/air-catfish/beesum-min/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Downloads YouTube subtitles with yt-dlp, parses WebVTT, and prints a clean
transcript or a summary. `transcript-only` (default) never sends the transcript
anywhere; Ollama stays local; API providers are opt-in.

<img src="docs/demos/real-video.gif" width="720" alt="beesum-min on a 3blue1brown video">

## Install

```powershell
dotnet tool install -g beesum-min
dotnet tool install -g beesum-mcp
dotnet add package Beesum.Core
```
## Source

Build from source:

```powershell
dotnet build beesum-min.slnx -c Release
dotnet run --project src\Beesum.Min.Cli -- --local-vtt samples\demo\talk.vtt
```

The CLI covers `--json`, `--stdin`, `--batch`, `--prompt-file`, `--timestamps`,
`--format mindmap`, `--from/--to`, `--save-vtt`, and `--save-transcript`; see
`--help` and the docs below. MCP lives in `beesum-mcp`.

## Docs

- [Embedding](docs/embedding.md) · [Prompts](docs/prompts.md) · [Editors](docs/editors.md)
- [MCP](docs/mcp.md) · [Packaging](docs/packaging.md) · [Testing](docs/testing.md)
- [Languages](docs/languages.md) · [Chunking](docs/chunking.md) · [For agents](docs/for-agents.md)

## License

MIT. Report security issues through GitHub advisories.
