# MCP server

`beesum-mcp` is a small Model Context Protocol server over stdio. It exposes the
same pipeline as the CLI as two tools, so an agent can summarize a YouTube video
or a transcript it already has.

## Install and configure

`beesum-mcp` is not on NuGet yet. From a source checkout:

```powershell
dotnet pack src\Beesum.Mcp\Beesum.Mcp.csproj -c Release -o artifacts\packages
dotnet tool install --tool-path .tools beesum-mcp --add-source artifacts\packages --version 0.2.0
```

Use `.tools/beesum-mcp` as the command until the package is published.

Claude Desktop / Cursor / any MCP client:

```json
{
  "mcpServers": {
    "beesum-min": {
      "command": "beesum-mcp",
      "env": {
        "BEESUM_PROVIDER": "transcript-only",
        "BEESUM_PROXY": "socks5://127.0.0.1:1080"
      }
    }
  }
}
```

Remove `BEESUM_PROXY` unless you need one. `BEESUM_PROVIDER` defaults to
`transcript-only`.

## Tools

### `summarize_url`

| Argument | Type | Meaning |
| --- | --- | --- |
| `url` | string, required | YouTube URL. |
| `style` | `historian` \| `programmer` \| `neutral` | Built-in prompt shortcut. |
| `format` | `markdown` \| `plaintext` \| `mindmap` | Output format. |
| `instructions` | string | Extra instructions appended to the prompt. |
| `timestamps` | boolean | Prefix cues with `[hh:mm:ss]` and ask the model to keep them. |
| `prompt` | string | Full system prompt; replaces style/format. |
| `provider` | string | Provider id, overrides `BEESUM_PROVIDER`. |

### `summarize_transcript`

| Argument | Type | Meaning |
| --- | --- | --- |
| `text` | string, required | Plain text or WebVTT. |
| `input_format` | `text` \| `vtt` | Default `text`. |
| `style`, `format`, `instructions`, `prompt`, `provider`, `timestamps` | | Same as above. |

Tool results are a single text content block with the summary. Expected failures
(`isError: true`) carry the pipeline's error message, so the agent does not need
to parse stderr.

## Environment

| Variable | Default | Purpose |
| --- | --- | --- |
| `BEESUM_PROVIDER` | `transcript-only` | Default provider id. |
| `BEESUM_OLLAMA_URL` | `http://localhost:11434` | Ollama base URL. |
| `BEESUM_OLLAMA_MODEL` | `llama3.2` | Ollama model. |
| `BEESUM_PROXY` | unset | Passed to yt-dlp as `--proxy`. |
| `BEESUM_SUB_LANGS` | `en` | Comma-separated subtitle languages. |
| `OPENAI_API_KEY` | unset | Enables the OpenAI-compatible provider. |
| `BEESUM_OPENAI_URL` / `BEESUM_OPENAI_MODEL` | OpenAI / `gpt-4o-mini` | Override endpoint. |
| `ANTHROPIC_API_KEY` | unset | Enables Anthropic. |
| `GEMINI_API_KEY` / `GOOGLE_API_KEY` | unset | Enables Gemini. |

API keys are read from the server environment only; they never appear in tool
arguments or protocol messages.

## Protocol

- newline-delimited JSON-RPC 2.0 on stdio;
- `initialize`, `ping`, `shutdown`, `tools/list`, `tools/call`;
- supported protocol versions: `2024-11-05`, `2025-03-26`, `2025-06-18`;
- stdout carries protocol messages only; logs go to stderr;
- no resources, prompts, sampling, or HTTP transport.

The server is meant for a local MCP client: one process, one user, one session.

## Try it by hand

```bash
printf '%s\n' \
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"shell","version":"1"}}}' \
  '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' \
  '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"summarize_transcript","arguments":{"text":"Hello world"}}}' \
  | beesum-mcp
```
