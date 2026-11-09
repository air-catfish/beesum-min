# beesum-min

Transcript-first YouTube summarizer for the terminal: **yt-dlp + WebVTT +
local or API LLMs**.

```bash
dotnet tool install -g beesum-min
beesum-min "https://www.youtube.com/watch?v=..." --timestamps
```

`transcript-only` is the default. Ollama stays local; cloud providers are
opt-in.

## Start

- [Install](install.md)
- [CLI](cli.md)
- [MCP](mcp.md)
- [Library](embedding.md)
- [Security](security.md)

The local core is host-agnostic; a server layer is not part of this repository.
