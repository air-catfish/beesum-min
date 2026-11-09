# CLI

```powershell
# Transcript only
beesum-min "https://www.youtube.com/watch?v=..." 

# Local Ollama
beesum-min "..." --provider ollama --ollama-model qwen3:1.7b --format markdown

# JSON for scripts
beesum-min "..." --json

# Clip and save
beesum-min "..." --from 00:12:34 --to 00:18:00 --timestamps `
  --save-vtt clip.vtt --save-transcript clip.md -o notes.md

# stdin
Get-Content captions.vtt | beesum-min --stdin --input vtt --timestamps

# Batch
beesum-min --batch urls.txt --jsonl
```

## Flags

| Flag | Meaning |
| --- | --- |
| `--provider` | `transcript-only`, `ollama`, `openai`, `anthropic`, `gemini` |
| `--style` | `historian`, `programmer`, `neutral` |
| `--format` | `markdown`, `plaintext`, `mindmap` |
| `--prompt-file` | full system-prompt override |
| `--instructions` | append to the built-in prompt |
| `--timestamps` | `[hh:mm:ss]` cues and timestamp-aware prompts |
| `--from` / `--to` | clip transcript by time |
| `--save-vtt` | copy the full VTT |
| `--save-transcript` | write the clipped transcript/summary |
| `--json` | one stable object |
| `--jsonl` | one object per batch item |
| `--stdin` | read a transcript from stdin |
| `--batch` | one URL per line |
| `--no-chunk` | disable map-reduce chunking |
| `--proxy` | passed to yt-dlp |

Progress goes to stderr; stdout is the result (or JSON).

## Doctor

```powershell
beesum-min doctor
beesum-min doctor --json
beesum-min doctor --check-updates
```

Checks yt-dlp, deno, temp, Ollama, and the selected provider's key. Update checks
are opt-in and never download anything.
