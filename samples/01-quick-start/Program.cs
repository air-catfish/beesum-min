using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;
using Microsoft.Extensions.DependencyInjection;

var vttPath = args.Length > 0 ? args[0] : "samples/demo/smoke.vtt";
if (!File.Exists(vttPath))
{
    Console.Error.WriteLine($"Subtitle file not found: {vttPath}");
    Console.Error.WriteLine("Usage: dotnet run --project samples/01-quick-start -- <path-to.vtt>");
    return 2;
}

var services = new ServiceCollection();
services.AddBeesum(options =>
{
    options.LlmProviderId = "transcript-only";
    options.OllamaBaseUrl = null; // keep the sample offline and dependency-free
});

await using var provider = services.BuildServiceProvider();
var pipeline = provider.GetRequiredService<ISummarizerPipeline>();

var progress = new Progress<PipelineStage>(stage => Console.Error.WriteLine($"[{stage}]"));
var result = await pipeline.RunAsync(
    new SummarizeCommand(
        Url: string.Empty,
        PromptStyle.Neutral,
        OutputFormat.Markdown,
        LocalVttPath: vttPath),
    progress);

if (!result.IsSuccess)
{
    Console.Error.WriteLine($"Error: {result.ErrorMessage}");
    return 1;
}

Console.WriteLine(result.Content);
Console.Error.WriteLine(
    $"provider={result.ProviderId} model={result.ModelName ?? "n/a"} duration={result.Duration}");

return 0;
