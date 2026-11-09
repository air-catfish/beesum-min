# beesum-min

Transcript-first YouTube summarizer for the terminal. **yt-dlp + WebVTT + local or
API LLMs.**

```powershell
dotnet tool install -g beesum-min
beesum-min "https://www.youtube.com/watch?v=..." --timestamps
```

The default provider, `transcript-only`, never sends the transcript anywhere.
`ollama` stays local. Cloud providers are opt-in.

## Start here

- [Install](install.md) — NuGet tool, Docker, source.
- [CLI](cli.md) — summarize, clip, save, JSON.
- [Library](embedding.md) — embed the pipeline in .NET.
- [MCP](mcp.md) — use it from Claude, Cursor, or another agent.
- [Editors](editors.md) — Vim, Emacs, pipes.
- [For agents](for-agents.md) — JSON, stdin, batch, exit codes.

## What it is not

- Not a hosted service; `beesum-min` is a local CLI/library.
- Not a GUI; the GUI lives separately.
- Not a promise that YouTube will never break — `yt-dlp` is maintained
  separately.

## Future server work

The local core is intentionally host-agnostic. The planned ASP.NET service and
chat connectors are documented under [Server](server/architecture.md); they are
not part of this repository.
