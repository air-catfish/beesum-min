# Adding an LLM provider

beesum-min talks to LLM backends through one small contract:

```csharp
public interface ILlmProvider
{
    string Id { get; }
    string DisplayName { get; }

    Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default);

    Task<SummaryResult> SummarizeAsync(
        SummaryRequest request,
        CancellationToken cancellationToken = default);
}
```

The pipeline resolves exactly one provider by `Id` (CLI `--provider <id>`, or
`BeesumOptions.LlmProviderId`) and calls `SummarizeAsync`. Everything else —
prompt style, output format, transcript text — arrives inside `SummaryRequest`.

Built-in implementations (`ollama`, `openai-compatible`) are **internal**: register
and select them through DI instead of constructing them directly.
`TranscriptOnlyLlmProvider` is public because it is useful on its own.

## Walkthrough: a local HTTP backend

The example below targets a fictional server that exposes `POST /v1/complete`
and `GET /health`. Real implementations should follow the same shape.

### 1. Implement the provider

Create the provider in your own project (not inside `Beesum.Core`):

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Beesum.Core.Abstractions;
using Beesum.Core.Llm;
using Beesum.Core.Models;

namespace MyApp;

public sealed class MyBackendLlmProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly string _model;

    public MyBackendLlmProvider(
        IHttpClientFactory httpClientFactory,
        string baseUrl,
        string model)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _httpClientFactory = httpClientFactory;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public string Id => "my-backend";
    public string DisplayName => "My Backend";

    public async Task<LlmAvailability> ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var client = _httpClientFactory.CreateClient("my-backend");
            using var response = await client.GetAsync($"{_baseUrl}/health", cts.Token);
            return response.IsSuccessStatusCode
                ? new LlmAvailability(true, ModelName: _model)
                : new LlmAvailability(false, $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return new LlmAvailability(false, "Probe timed out.");
        }
        catch (Exception ex)
        {
            return new LlmAvailability(false, ex.Message);
        }
    }

    public async Task<SummaryResult> SummarizeAsync(
        SummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            var client = _httpClientFactory.CreateClient("my-backend");
            var payload = new
            {
                model = _model,
                prompt = LlmPrompts.BuildSystemPrompt(request)
                    + "\n\n" + request.Transcript.ToPlainText(),
            };

            using var response = await client.PostAsJsonAsync(
                $"{_baseUrl}/v1/complete", payload, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                return new SummaryResult(false, string.Empty, Id, $"HTTP {(int)response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cts.Token));
            var content = doc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            return new SummaryResult(
                true,
                content,
                Id,
                ModelName: doc.RootElement.TryGetProperty("model", out var m) ? m.GetString() : _model);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            return new SummaryResult(false, string.Empty, Id, "Summarize timed out.");
        }
        catch (Exception ex)
        {
            return new SummaryResult(false, string.Empty, Id, ex.Message);
        }
    }
}
```

Notes:

* Register the HTTP client under a unique name; `IHttpClientFactory` clients are
  cheap and thread-safe.
* `ProbeAsync` is informational today; the pipeline calls `SummarizeAsync`.
* Return failures as `SummaryResult(false, ..., error)`; do not throw for expected
  network errors. The pipeline and both hosts display `ErrorMessage`.
* Fill `ModelName`, `Usage` (`TokenUsage`), and `Duration` when your backend
  reports them — CLI `--json` exposes these fields.
* Respect `SummaryRequest.Instructions` and `SummaryRequest.SystemPrompt`.
  `LlmPrompts.BuildSystemPrompt(request)` already does that.
* Never log API keys or transcript content.

`LlmPrompts.BuildSystemPrompt(request)` is public, so your provider does not have
to reimplement style, format, or instruction handling.

### 2. Register it in DI

```csharp
services.AddBeesum(options =>
{
    options.LlmProviderId = "my-backend";
    options.OllamaBaseUrl = null; // only if you do not want the local provider
});

services.AddHttpClient("my-backend");
services.AddSingleton<ILlmProvider>(sp =>
    new MyBackendLlmProvider(
        sp.GetRequiredService<IHttpClientFactory>(),
        "http://localhost:8080",
        "my-model"));
```

The pipeline factory enumerates `ILlmProvider` registrations when it is resolved,
so the order of `AddBeesum` and your registration does not matter. The provider
list is visible to hosts through DI; a UI can build its dropdown from
`IEnumerable<ILlmProvider>` without changes.

To switch providers at run time (instead of configuring one in `BeesumOptions`),
use `ISummarizerPipelineFactory`:

```csharp
var pipeline = factory.Create("my-backend");
```

### 3. Test it

Use `Beesum.Testing` (`InMemoryHttpMessageHandler`, `InMemoryHttpClientFactory`,
`InMemoryLlmProvider`) or plain fakes; no mocking library is required:

```csharp
using Beesum.Testing;

var handler = new InMemoryHttpMessageHandler()
    .EnqueueJson("""{"text":"summary"}""");
var provider = new MyBackendLlmProvider(
    new InMemoryHttpClientFactory(handler), "http://localhost:9999", "m");

var result = await provider.SummarizeAsync(
    new SummaryRequest(transcript, PromptStyle.Neutral, OutputFormat.PlainText));

Assert.True(result.IsSuccess);
Assert.Equal("summary", result.Content);
Assert.Equal("my-backend", result.ProviderId);
```

Cover at least: success, non-2xx response, malformed JSON, timeout, and
`ProbeAsync`.

### 4. Verify manually

```powershell
dotnet run --project src\Beesum.Min.Cli -- --local-vtt .\smoke.vtt --provider my-backend
```

Because the provider list is resolved through DI, forgetting step 2 produces
`LLM provider 'my-backend' is not registered.`

## Checklist

- [ ] `Id` is stable, lowercase, and unique.
- [ ] All HTTP calls have timeouts and honor the cancellation token.
- [ ] Expected failures return `SummaryResult(false, ...)`; exceptions are reserved
      for bugs.
- [ ] Metadata (`ModelName`, `Usage`, `Duration`) is filled when available.
- [ ] No secrets or transcript text in logs or exception messages.
- [ ] Registered in DI and reachable from the CLI (`--provider`).
- [ ] Tests cover success, HTTP error, bad JSON, and timeout.
- [ ] `dotnet build` and the test projects are clean.
