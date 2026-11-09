# Beesum.Testing

In-memory test doubles for [Beesum.Core](https://www.nuget.org/packages/Beesum.Core).

```csharp
using Beesum.Core.DependencyInjection;
using Beesum.Testing;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBeesum(options => options.OllamaBaseUrl = null);
services.AddBeesumTesting();

await using var provider = services.BuildServiceProvider();
```

`AddBeesumTesting()` replaces the storage, yt-dlp, parser, process-runner, and LLM
registrations with in-memory implementations, so tests need no disk, network, yt-dlp,
or Ollama.

Included doubles:

* `InMemorySettingsStore`, `InMemorySecretStore`, `InMemoryTranscriptCache`,
  `InMemorySummaryHistory`
* `InMemoryYtDlpService`, `InMemoryYtDlpPathResolver`, `InMemorySubtitleParser`
* `InMemoryProcessRunner`
* `InMemoryLlmProvider`
* `InMemoryHttpMessageHandler`, `InMemoryHttpClientFactory`

## License

MIT.
