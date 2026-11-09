# Beesum.Core

Transcript-first YouTube summarizer core for .NET 10.

Beesum.Core downloads subtitles with [yt-dlp](https://github.com/yt-dlp/yt-dlp), parses
WebVTT (including YouTube auto-generated captions), and summarizes the transcript with
a local LLM or without an LLM at all. It ships no UI and makes no network calls during
DI registration or startup.

## Install

```powershell
dotnet add package Beesum.Core
```

## Quick start

```csharp
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBeesum(options =>
{
    options.LlmProviderId = "transcript-only";
    options.OllamaBaseUrl = null; // do not register the local Ollama provider
});

await using var provider = services.BuildServiceProvider();
var pipeline = provider.GetRequiredService<ISummarizerPipeline>();

var result = await pipeline.RunAsync(
    new SummarizeCommand(
        Url: string.Empty,
        PromptStyle.Neutral,
        OutputFormat.Markdown,
        LocalVttPath: "captions.vtt"));

Console.WriteLine(result.IsSuccess ? result.Content : result.ErrorMessage);
```

`SummaryResult` also carries `ProviderId`, `ModelName`, `Usage` (TokenUsage), and
`Duration` when the provider reports them.

## Features

* WebVTT parser for manual and auto-generated captions, including the rolling-window
  format and inline styling.
* yt-dlp integration with proxy, cookies, and JS-runtime passthrough.
* `ILlmProvider` extension point with built-in `transcript-only`, Ollama, and
  OpenAI-compatible providers.
* `ISummarizerPipelineFactory` for choosing a provider at run time.
* Optional `ILogger<T>` integration; no-op logging when logging is not configured.
* `Beesum.Testing` package with in-memory doubles for hermetic tests.

## Documentation

See [docs/embedding.md](https://github.com/air-catfish/beesum-min/blob/main/docs/embedding.md)
and [docs/adding-a-provider.md](https://github.com/air-catfish/beesum-min/blob/main/docs/adding-a-provider.md).

## License

MIT.
