# Embedding Beesum.Core

This guide shows how to use the Core library from your own application: register the
services, run the pipeline, read the result metadata, plug in a custom LLM provider,
and test everything without network access.

The package targets .NET 10 and follows `0.x` versioning: the public API may still
change between minor releases.

## 1. Install

```powershell
dotnet add package Beesum.Core
dotnet add package Beesum.Testing   # in test projects
```

## 2. Register the services

```csharp
using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddBeesum(options =>
{
    options.LlmProviderId = "transcript-only"; // or "ollama"
    options.OllamaBaseUrl = "http://localhost:11434"; // set to null to skip Ollama
    options.OllamaModel = "llama3.2";
});

await using var provider = services.BuildServiceProvider();
var pipeline = provider.GetRequiredService<ISummarizerPipeline>();
```

`AddBeesum` registers contracts and their default implementations and exposes the same
values as `IOptions<BeesumOptions>`:

```csharp
var options = provider.GetRequiredService<IOptions<BeesumOptions>>().Value;
```

The container structure (which providers exist, where yt-dlp comes from) is decided at
registration time. Mutating the options object afterwards does not re-register services.

### Options

| Property | Default | Purpose |
| --- | --- | --- |
| `YtDlpPath` | `null` | Explicit yt-dlp path; `null` auto-detects settings → `tools/` → `PATH`. |
| `OllamaBaseUrl` | `http://localhost:11434` | Ollama URL; `null`/empty disables the provider. |
| `OllamaModel` | `llama3.2` | Ollama model name. |
| `OpenAiBaseUrl` | `null` | OpenAI-compatible URL; `null`/empty disables the provider. |
| `OpenAiModel` | `gpt-4o-mini` | OpenAI-compatible model name. |
| `OpenAiApiKeyProvider` | `null` | API-key callback; `null` reads `openai.apiKey` from `ISecretStore`. |
| `LlmProviderId` | `transcript-only` | Provider used by the default pipeline. |
| `SubtitlesDirectory` | `null` | Persistent subtitle directory; `null` uses a temp directory. |
| `StorageRoot` | platform default | Root for settings, secrets, cache, and history. |
| `YtDlpExtraArgs` | empty | Extra arguments for every yt-dlp call (`--proxy`, cookies, ...). |
| `SubtitleLanguages` | `en` | yt-dlp `--sub-langs` selectors, in preference order. |
| `ChunkLongTranscripts` | `true` | Split transcripts over `ChunkTokens` and combine partial summaries. |
| `ChunkTokens` | `6000` | Approximate input budget per chunk. |

## 3. Run the pipeline

### From a local subtitle file

```csharp
var result = await pipeline.RunAsync(
    new SummarizeCommand(
        Url: string.Empty,
        PromptStyle.Neutral,
        OutputFormat.Markdown,
        LocalVttPath: "captions.vtt"),
    progress: null,
    cancellationToken: CancellationToken.None);

if (!result.IsSuccess)
{
    Console.Error.WriteLine(result.ErrorMessage);
    return;
}

Console.WriteLine(result.Content);
```

### From a YouTube URL

```csharp
var result = await pipeline.RunAsync(
    new SummarizeCommand(
        Url: "https://www.youtube.com/watch?v=...",
        PromptStyle.Historian,
        OutputFormat.Markdown),
    cancellationToken: cancellationToken);
```

The URL path needs `yt-dlp` to be installed (or pointed to by `YtDlpPath`). The
library never starts a download on its own: a call to `RunAsync` is the user action.

### Progress

Stages are reported in order: `Starting`, `Downloading`, `Parsing`, `Summarizing`,
`Completed`. `Completed` fires only on success.

```csharp
var progress = new Progress<PipelineStage>(stage => Console.WriteLine(stage));
await pipeline.RunAsync(command, progress);
```

`IProgress<T>` callbacks may run on the captured synchronization context; pass a
synchronous implementation in tests if order matters.

## 4. Read the result

`SummaryResult` is a record:

| Member | Meaning |
| --- | --- |
| `IsSuccess` | Whether a summary was produced. |
| `Content` | Summary text, or empty on failure. |
| `ProviderId` | Provider that produced the result; `null` if the pipeline failed early. |
| `ErrorMessage` | Human-readable failure reason on failure. |
| `ModelName` | Model reported by the provider, when known. |
| `Usage` | `TokenUsage` (prompt/completion/total), when the backend reports it. |
| `Duration` | Time spent in the provider call. |

Expected failures (missing subtitles, HTTP errors, timeouts) come back as
`IsSuccess == false`; exceptions are reserved for programming errors and cancellation.

## 5. Choose a provider at run time

`ISummarizerPipelineFactory` creates a pipeline for a provider id:

```csharp
var factory = provider.GetRequiredService<ISummarizerPipelineFactory>();
var ollama = factory.Create("ollama");
```

Use it when the provider is selected dynamically (a UI dropdown, a batch job with mixed
settings). Unknown ids fail inside `RunAsync` with a descriptive error.

## 6. Custom prompts

`SummaryRequest` accepts two optional fields:

```csharp
new SummaryRequest(
    transcript,
    PromptStyle.Neutral,
    OutputFormat.Markdown,
    Instructions: "Five bullets, no fluff.",
    SystemPrompt: null); // set to replace the built prompt entirely
```

`Instructions` is appended to the style/format prompt. `SystemPrompt`, when set,
wins and the style/format text is ignored. The same fields exist on
`SummarizeCommand`, which is what the CLI uses.

`LlmPrompts.BuildSystemPrompt(request)` applies the rules; custom providers should
call it instead of rebuilding the prompt.

## 7. Custom providers

Implement `ILlmProvider`, register it after `AddBeesum`, and point
`LlmProviderId`/`Create` at its `Id`. Reuse the public prompt builder
`LlmPrompts.BuildSystemPrompt(style, format)`. See
[adding-a-provider.md](adding-a-provider.md) for a full walkthrough.

Built-in implementations are internal; register and select them through DI.

## 8. Logging

Core services accept `ILogger<T>` and default to `NullLogger`. Register logging before
building the provider:

```csharp
services.AddLogging(builder => builder.AddConsole());
```

No transcript content, API keys, or cookies are logged.

## 9. Testing

```csharp
var services = new ServiceCollection();
services.AddBeesum(options => options.OllamaBaseUrl = null);
services.AddBeesumTesting();

await using var provider = services.BuildServiceProvider();
```

`AddBeesumTesting()` replaces storage, yt-dlp, parser, process runner, and providers
with in-memory doubles. Override a single double afterwards by registering it last:

```csharp
services.AddSingleton<ILlmProvider>(new InMemoryLlmProvider("ollama"));
```

`InMemoryProcessRunner` records every invocation, so you can assert the exact argument
list (the library always uses `ProcessStartInfo.ArgumentList`).

## 10. Storage

The default root is `%APPDATA%\Beesum` on Windows and `~/.config/Beesum` elsewhere.
It contains `settings.json`, `secrets.dat`, `transcripts/`, and `history.jsonl`.
At the moment the pipeline consumes only the `ytdlp.path` setting; the cache and
history contracts exist for hosts to use.

`DpapiSecretStore` uses DPAPI on Windows; on other platforms it stores secrets in
plain files in the same directory, so keep that path private.

## 11. Lifetime and threading

All registered services are singletons and stateless per request: a single pipeline
can process independent commands concurrently. `SummaryResult` is immutable; pass
`CancellationToken` to stop work promptly.
