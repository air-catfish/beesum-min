# Prompts

The three built-in styles (`historian`, `programmer`, `neutral`) are shortcuts, not
the product. Anything real goes through a prompt file or extra instructions.

## Two knobs

`--instructions "<text>"` appends to the built-in style/format prompt:

```powershell
beesum-min "..." --instructions "Five bullets for a team lead, no fluff."
```

`--prompt-file <path>` replaces the whole system prompt:

```powershell
beesum-min "..." --prompt-file examples/prompts/tech-lead.md
```

When a prompt file is set, `--style` and `--format` no longer shape the prompt.
The transcript is still sent separately as the user message, so never paste the
transcript into the prompt file.

From code, the same two fields live on `SummaryRequest`:

```csharp
new SummaryRequest(
    transcript,
    PromptStyle.Neutral,
    OutputFormat.Markdown,
    Instructions: "Five bullets.",
    SystemPrompt: null);
```

## Output formats

`--format` selects the prompt shape:

- `markdown` — headings and bullets;
- `plaintext` — no markup;
- `mindmap` — the model returns a nested bullet list; beesum-min wraps it in a valid Mermaid `mindmap`.

`mindmap` needs an LLM provider; `transcript-only` always prints the transcript.
Add `--timestamps` to prefix cues with `[hh:mm:ss]` and ask the model to keep
those times when it cites a moment.
## Examples

- `examples/prompts/tech-lead.md` — five bullets, decisions and numbers.
- `examples/prompts/eli5.md` — plain-language explanation.
- `examples/prompts/meeting-notes.md` — decisions, actions, open questions.

Write prompts that say what to leave out. "Summarize" is not an instruction;
"five bullets, no preamble, keep numbers" is.

## Chunking

With chunking enabled, a full `SystemPrompt` is used for every chunk and for the
final combination. A prompt that only makes sense for a finished summary will
produce strange partial summaries. `Instructions` are applied to the final
combination; the map phase uses a fixed part-summary instruction.

## Safety

- Do not put API keys, cookies, or personal data in prompt files.
- Do not ask the model to output secrets it saw in the transcript; it will.
- A prompt can change tone and structure, not facts. If the transcript does not
  contain a number, the model must not invent one.
