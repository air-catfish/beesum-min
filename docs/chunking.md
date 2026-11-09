# Chunking long transcripts

Providers have input limits. Instead of failing on a two-hour podcast, the
pipeline splits the transcript, summarizes each part, and combines the parts.

## When it happens

Chunking is on by default. It triggers when the estimated transcript size exceeds
`ChunkTokens` (default `6000`). The estimate is deliberately rough: characters
divided by three, not a real tokenizer. Latin text is over-estimated, Cyrillic and
CJK are closer to the truth. Over-estimating means one extra chunk, which is
cheaper than a context overflow.

CLI controls:

```powershell
beesum-min "..." --chunk-tokens 4000
beesum-min "..." --no-chunk
```

Library controls:

```csharp
services.AddBeesum(options =>
{
    options.ChunkLongTranscripts = true;
    options.ChunkTokens = 4000;
});
```

## How it works

1. **Map.** The transcript is cut on segment boundaries; an oversized segment is
   cut on word boundaries. Each chunk gets a fixed instruction: summarize this
   part, keep names, numbers, decisions, and conclusions.
2. **Reduce.** The partial summaries are combined with the original style,
   format, and instructions. If the partials themselves exceed `ChunkTokens`,
   they are grouped and reduced again, up to three levels.
3. **Result.** `SummaryResult` contains the final text, the summed `Usage`, the
   total `Duration`, and the model reported by the final call.

Progress reports `Chunking` once, then `Completed` when the final summary is
ready. A failed chunk fails the whole run with `Chunk <i>/<n> failed: ...`; the
pipeline does not silently combine incomplete input.

## Cost and privacy

Every chunk is a provider call. With a cloud API that means more tokens and more
money; `--json` shows the total in `usage` so you can measure it. With
`transcript-only` and `ollama` nothing leaves the machine, but chunks still mean
extra calls.

A full `SystemPrompt` override applies to both map and reduce calls, so a custom
prompt must make sense for a partial summary as well as for the final answer.
`Instructions` apply to the final combination; map calls use the fixed
part-summary instruction.

## Limitations

* The estimate is not a tokenizer; very unusual languages can be under-counted.
* Timestamps are not preserved in summaries; chunks carry the original segment
  times only as metadata.
* A single chunk larger than the model context is still possible if the provider
  rejects it; there is no automatic retry with a smaller chunk.
