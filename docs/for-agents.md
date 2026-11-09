# For LLM agents and scripts

The CLI is built to be called by a program, not only by a person. Everything it
prints is either the summary or a line of progress on stderr.

## Contract

- `--json` prints one object; `--jsonl` prints one compact object per batch item.
- Exit codes: `0` success, `1` runtime failure, `2` invalid arguments.
- Progress goes to stderr; stdout stays parseable.
- `--stdin --input text|vtt` accepts a transcript from another tool.
- `--batch <file>` takes one URL per line, skips blanks and `#` comments.
- `--prompt-file` replaces the system prompt; `--instructions` appends to it.
- API keys are read from environment variables, never from arguments.

The JSON shape is documented in [json-output.md](json-output.md), including the
batch object with its `url` field.

## Recipes

```bash
# Get only the text
beesum-min "https://www.youtube.com/watch?v=..." --json | jq -r '.content'

# Fail the script when the summary failed
beesum-min "..." --json | jq -e '.isSuccess' >/dev/null

# Summarize a transcript produced by another tool
cat transcript.vtt | beesum-min --stdin --input vtt --json > out.json

# Batch, with per-item URLs in the output
beesum-min --batch urls.txt --jsonl > results.jsonl
```

## What an agent should not do

- Do not re-summarize the returned `content` with another model; you already have
  the summary.
- Do not paste the transcript into `--prompt-file`; it is sent separately as the
  user message and will be duplicated.
- Do not pass API keys on the command line; they end up in shell history and
  process listings. Use the provider's environment variable.
- Do not ignore `isSuccess`; `content` is an empty string on failure.

## Why this is agent-friendly

- No telemetry, no call-home, no background network activity.
- `transcript-only` needs no model and no network beyond yt-dlp.
- Chunking keeps long transcripts inside the model's context.
- `usage` in JSON is the sum of every provider call, so cost is measurable.
- The test suite is offline and deterministic; CI runs on Windows and Linux.

For a native agent interface use `beesum-mcp`: it exposes the same pipeline as
`summarize_url` and `summarize_transcript` over MCP. The JSON/stdin contract above
remains the stable fallback for scripts.
