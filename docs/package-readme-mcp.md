<!-- mcp-name: io.github.air-catfish/beesum-min -->
# beesum-mcp

Read YouTube transcripts from any MCP client. `beesum-mcp` downloads subtitles with
[yt-dlp](https://github.com/yt-dlp/yt-dlp) and gives your agent clean, timestamped
text. The transcript stays on your machine unless you configure a cloud LLM provider.

## Quick start

Requirements: the .NET 10 runtime and `yt-dlp` on your `PATH`.

```powershell
dotnet tool install -g beesum-mcp
```

**Claude Desktop / Cursor**

```json
{
  "mcpServers": {
    "beesum-min": {
      "command": "beesum-mcp"
    }
  }
}
```

**VS Code** (`.vscode/mcp.json`)

```json
{
  "servers": {
    "beesum-min": {
      "type": "stdio",
      "command": "beesum-mcp"
    }
  }
}
```

Then ask your agent: *"Get the transcript of https://www.youtube.com/watch?v=VIDEO_ID
and list the key points."*

## Tools

| Tool | Available | Purpose |
| --- | --- | --- |
| `get_transcript` | always | Download YouTube subtitles and return the transcript as text. Your model does the summarizing. |
| `summarize_url` | only with an LLM provider | Download subtitles and summarize them with the configured provider. |
| `summarize_transcript` | only with an LLM provider | Summarize plain text or WebVTT you already have. |

By default (`BEESUM_PROVIDER` unset or `transcript-only`) the server lists only
`get_transcript`. The summarize tools appear when `BEESUM_PROVIDER` selects
`ollama`, `openai-compatible`, `anthropic` or `gemini`.

`get_transcript` arguments:

- `url` (required): a youtube.com, youtu.be, m.youtube.com or music.youtube.com link.
- `start`, `end`: optional clip range as `ss`, `mm:ss` or `hh:mm:ss`.
- `lang`: optional subtitle language code(s), comma-separated, for example `ru` or `ru,en`
  (up to 5). Defaults to `BEESUM_SUB_LANGS`. If the video has no subtitles in that
  language, the error lists the manual subtitles and original-language automatic
  captions it does have.
- `offset`, `limit`: paging in characters (default limit 12000, maximum 20000). When
  the text is longer, the result ends with the `offset` to request next.

## Configuration

Environment variables on the MCP server entry:

| Variable | Default | Purpose |
| --- | --- | --- |
| `BEESUM_PROVIDER` | `transcript-only` | Which LLM provider the summarize tools use. |
| `BEESUM_SUB_LANGS` | `en` | Comma-separated subtitle languages, for example `ru,en`. |
| `BEESUM_PROXY` | unset | Proxy passed to yt-dlp (`--proxy`). |
| `BEESUM_OLLAMA_URL` / `BEESUM_OLLAMA_MODEL` | `http://localhost:11434` / `llama3.2` | Local Ollama. |
| `BEESUM_OLLAMA_NUM_CTX` | `8192` | Ollama context window in tokens. Raise it for long chunks. |
| `BEESUM_MAX_TOKENS` | `1024` | Output token limit for cloud providers. |
| `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY` | unset | Enable the matching cloud provider. |

API keys are read from the server environment only; they never appear in tool
arguments or protocol messages.

## Good to know

- Transcript text is **untrusted data** from the video's author. Agents should not
  follow instructions found inside it.
- No subtitles for the requested language means no transcript. Set
  `BEESUM_SUB_LANGS` to the languages you need.
- YouTube can block requests from some networks; a proxy (`BEESUM_PROXY`) may help.
- The server speaks MCP over stdio for a local client. There is no HTTP transport.

## Links

[Source and issues](https://github.com/air-catfish/beesum-min) ·
[Full MCP guide](https://github.com/air-catfish/beesum-min/blob/main/docs/mcp.md) ·
[Changelog](https://github.com/air-catfish/beesum-min/blob/main/CHANGELOG.md)

MIT.
