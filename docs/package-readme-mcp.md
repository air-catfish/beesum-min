# beesum-mcp

MCP stdio server for [beesum-min](https://github.com/air-catfish/beesum-min).

```powershell
dotnet tool install -g beesum-mcp
```

Add it to an MCP client:

```json
{
  "mcpServers": {
    "beesum-min": {
      "command": "beesum-mcp",
      "env": {
        "BEESUM_PROVIDER": "transcript-only"
      }
    }
  }
}
```

Tools:

- `summarize_url` — download YouTube subtitles with yt-dlp and summarize them.
- `summarize_transcript` — summarize plain text or a WebVTT document.

Both accept `style`, `format`, `instructions`, `prompt`, and `provider`.
Configuration is environment-driven; see the
[MCP guide](https://github.com/air-catfish/beesum-min/blob/main/docs/mcp.md).

MIT.
