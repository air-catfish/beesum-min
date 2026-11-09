# JSON output

`beesum-min --json` writes one JSON object to stdout (or to the file passed with
`-o/--output`) instead of human-readable text. Progress messages go to stderr, so
stdout stays valid JSON.

## Invocation

```powershell
beesum-min --local-vtt .\captions.vtt --json
beesum-min "https://www.youtube.com/watch?v=..." --json -o result.json
```

Exit codes are unchanged: `0` success, `1` runtime failure (the object has
`"isSuccess": false`), `2` invalid arguments (no JSON is produced).

## Shape

```json
{
  "isSuccess": true,
  "content": "Summary text",
  "providerId": "ollama",
  "errorMessage": null,
  "modelName": "llama3.2",
  "usage": {
    "promptTokens": 120,
    "completionTokens": 80,
    "totalTokens": 200
  },
  "durationMs": 842.5
}
```

| Field | Type | Notes |
| --- | --- | --- |
| `isSuccess` | boolean | `false` for expected failures (network, no subtitles, model errors). |
| `content` | string | Summary text; an empty string on failure. |
| `providerId` | string \| null | Provider that produced the result; `null` if the pipeline failed before invoking one. |
| `errorMessage` | string \| null | Failure reason; `null` on success. |
| `modelName` | string \| null | Model reported by the provider; `null` when unknown (for example `transcript-only`). |
| `usage` | object \| null | Token counts when the backend reports them; `null` otherwise. |
| `durationMs` | number \| null | Time spent in the provider call, in milliseconds (may be fractional); `null` on failure. |

All fields are always present; missing data is `null`.

## Failure example

```json
{
  "isSuccess": false,
  "content": "",
  "providerId": "ollama",
  "errorMessage": "model 'llama3.2' not pulled",
  "modelName": "llama3.2",
  "usage": null,
  "durationMs": null
}
```

## Notes

* The schema is additive within `0.x`: new optional fields may appear, existing
  fields keep their names and types.
* `--json` implies quiet progress on stdout, but progress lines still appear on
  stderr unless the caller discards stderr.
* With `-o`, the file contains JSON; the confirmation message is written to stderr.
* The same data is available programmatically through `SummaryResult` in
  `Beesum.Core` (see [embedding.md](embedding.md)).

## Batch

`--batch <file>` produces an array by default and JSON Lines with `--jsonl`.
Each object has one extra field, `url`, at the front:

```json
[
  {
    "url": "https://www.youtube.com/watch?v=abc",
    "isSuccess": true,
    "content": "Summary text",
    "providerId": "ollama",
    "errorMessage": null,
    "modelName": "llama3.2",
    "usage": { "promptTokens": 120, "completionTokens": 80, "totalTokens": 200 },
    "durationMs": 842.5
  }
]
```

`--jsonl` writes one compact object per line instead of an array:

```json
{"url":"https://...","isSuccess":true,"content":"...","providerId":"ollama","errorMessage":null,"modelName":"llama3.2","usage":null,"durationMs":12.4}
```

Batch mode does not add a `url` field to single-URL output; the schema above is
for batch only.