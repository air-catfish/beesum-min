# Changelog

All notable changes are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-11-09

### Added

- `Beesum.Core` NuGet package with `AddBeesum(Action<BeesumOptions>)`,
  `IOptions<BeesumOptions>`, optional `ILogger<T>`, and
  `ISummarizerPipelineFactory`.
- `Beesum.Testing` with in-memory doubles and `AddBeesumTesting()`.
- CLI `beesum-min` with `--json`, `--jsonl`, `--stdin`, `--batch`, and
  `--instructions` / `--prompt-file`.
- Providers: `transcript-only`, Ollama, OpenAI-compatible, Anthropic, Gemini.
- API keys from environment variables only; keyless local OpenAI-compatible
  endpoints.
- Retries for HTTP 429/5xx with `Retry-After`, two-minute request timeout.
- `--from` / `--to` clip the transcript by time; `--save-vtt` and
  `--save-transcript` keep the raw subtitles and the clipped text.
- Interactive save prompts (once per file) with `--no-save-prompt` for scripts.
- NuGet publishing uses OIDC Trusted Publishing (`NuGet/login@v1`); no long-lived
  `NUGET_API_KEY`.
- MCP rejects non-YouTube URLs; Docker runs non-root with pinned checksums; Unix
  storage files are private.
- `--timestamps` for `[hh:mm:ss]` transcript output and timestamp-aware prompts.
- `--format mindmap` for a Mermaid mindmap, with a local Markdown-to-Mermaid formatter.
- `beesum-mcp` stdio MCP server with `summarize_url` and `summarize_transcript`.
- `--sub-langs` and `BeesumOptions.SubtitleLanguages`; the default is now plain
  `en`, and multiple downloaded subtitles are picked in preference order.
- Chunked map-reduce summarization for long transcripts (`--chunk-tokens`,
  `--no-chunk`); usage and duration are summed across calls.
- CI on Windows and Linux, NuGet/GHCR release workflow, Docker image.
- Docs: embedding guide, API providers, JSON schema, editor integration.

### Fixed

- `%TEMP%`-unwritable fallback (BEESUM-001).
- A UTF-8 BOM prepended by Windows pipes to `--stdin`.
- yt-dlp discovery no longer shells out to `which`/`where`; minimal Linux images
  often do not have either binary.
- Empty or broken subtitles fail with "No subtitle text found" instead of sending
  an empty transcript to the model.
- A UTF-8 BOM at the start of a batch file no longer corrupts the first URL.

### Changed

- `Beesum.Core` implementations are `internal`; interfaces, models, and DI are
  the public surface.

[Unreleased]: https://github.com/air-catfish/beesum-min/compare/main...HEAD
