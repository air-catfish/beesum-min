# API providers

beesum-min can summarize with a local model or a cloud API. Cloud keys are read
from environment variables only — passing a key as an argument puts it in your
shell history and in the process list.

| `--provider` | API | Key variable | Default endpoint |
| --- | --- | --- | --- |
| `openai` | OpenAI-compatible | `OPENAI_API_KEY` | `https://api.openai.com` |
| `anthropic` (alias `claude`) | Anthropic Messages | `ANTHROPIC_API_KEY` | `https://api.anthropic.com` |
| `gemini` (alias `google`) | Google Gemini | `GEMINI_API_KEY` or `GOOGLE_API_KEY` | `https://generativelanguage.googleapis.com` |

The OpenAI-compatible provider is the lowest common denominator: it works with
OpenAI, OpenRouter, Groq, Together, LM Studio, llama.cpp server, and anything else
that speaks `/v1/chat/completions`.

## Examples

```powershell
# OpenAI
$env:OPENAI_API_KEY = "sk-..."
beesum-min "https://www.youtube.com/watch?v=..." --provider openai

# OpenRouter
$env:OPENROUTER_API_KEY = "sk-or-..."
beesum-min "https://www.youtube.com/watch?v=..." --provider openai `
  --api-url https://openrouter.ai/api `
  --api-model anthropic/claude-sonnet-4-5 `
  --api-key-env OPENROUTER_API_KEY

# Groq
$env:GROQ_API_KEY = "gsk_..."
beesum-min "..." --provider openai `
  --api-url https://api.groq.com/openai `
  --api-model llama-3.3-70b-versatile `
  --api-key-env GROQ_API_KEY

# Local LM Studio (no key needed)
beesum-min "..." --provider openai `
  --api-url http://localhost:1234 `
  --api-model qwen2.5-7b-instruct

# Anthropic
$env:ANTHROPIC_API_KEY = "sk-ant-..."
beesum-min "..." --provider anthropic
beesum-min "..." --provider anthropic --api-model claude-sonnet-4-5 --max-tokens 2048

# Gemini
$env:GEMINI_API_KEY = "..."
beesum-min "..." --provider gemini
beesum-min "..." --provider gemini --api-model gemini-2.5-flash
```

`--api-url`, `--api-model`, and `--api-key-env` apply to the selected API provider
and ignore the defaults above. `--max-tokens` is used by Anthropic and Gemini
(Anthropic requires it; Gemini uses it as `maxOutputTokens`).

## Retries and timeouts

* Requests are attempted up to three times on HTTP 429 and 5xx.
* `Retry-After` is honored, capped at five seconds.
* A request has a two-minute timeout; cancellation is passed through.

## Errors

| Situation | What you get |
| --- | --- |
| Key variable is not set | `No API key for <provider>. Set ...` and exit code `2` |
| Key is rejected | `invalid API key` in the result / JSON |
| Rate limit survives retries | `rate limited; retry later` |
| Model name is wrong | the provider's HTTP error, truncated to 300 chars |
| Prompt blocked (Gemini) | `Prompt blocked: <reason>` |

With `--json`, the same text is in `errorMessage`; exit code `1`.

## From the library

```csharp
services.AddBeesum(options =>
{
    options.LlmProviderId = "anthropic";
    options.AnthropicBaseUrl = "https://api.anthropic.com";
    options.AnthropicModel = "claude-sonnet-4-5";
    options.AnthropicApiKeyProvider = _ =>
        Task.FromResult(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
    options.MaxOutputTokens = 2048;
});
```

`GeminiBaseUrl`/`GeminiModel`/`GeminiApiKeyProvider` work the same way. The
built-in providers are `internal`: configure them through `BeesumOptions` instead
of constructing them.

## Notes

* Model names and prices change. The defaults are a starting point, not a promise.
* API providers see the transcript. `transcript-only` and `ollama` do not leave
  your machine.
* A 200 response with an empty body is treated as a failure, not as an empty
  summary.
