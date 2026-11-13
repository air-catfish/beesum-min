using Microsoft.Extensions.DependencyInjection;
using Beesum.Min.Cli;
using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Core.Models;

try
{
    const string TruncationWarning =
        "Warning: the summary may be incomplete (the model reached its output or context limit). " +
        "Raise --max-tokens, or set BEESUM_OLLAMA_NUM_CTX for Ollama.";

    var options = CliOptionsParser.Parse(args);

    if (options.ShowHelp)
    {
        Console.WriteLine(HelpText.Text);
        return 0;
    }

    if (options.ShowVersion)
    {
        Console.WriteLine($"beesum-min {CliVersion.Value}");
        return 0;
    }

    if (options.Doctor)
    {
        var doctorServices = new ServiceCollection();
        doctorServices.AddBeesum(beesum =>
        {
            beesum.YtDlpPath = options.YtDlpPath;
            beesum.OllamaBaseUrl = options.OllamaBaseUrl;
        });

        await using var doctorProvider = doctorServices.BuildServiceProvider();
        return await Doctor.RunAsync(options, doctorProvider, Console.Out, CancellationToken.None);
    }

    var config = CliConfiguration.Resolve(options);
    options = CliSavePrompts.Apply(options);

    string? systemPrompt = null;
    if (options.PromptFile is not null)
    {
        if (!File.Exists(options.PromptFile))
            throw new ArgumentException($"Prompt file not found: {options.PromptFile}");

        systemPrompt = await File.ReadAllTextAsync(options.PromptFile);
        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException($"Prompt file is empty: {options.PromptFile}");
    }

    var services = new ServiceCollection();
    services.AddBeesum(beesum =>
    {
        beesum.YtDlpPath = options.YtDlpPath;
        beesum.MaxOutputTokens = config.MaxTokens;
        beesum.ChunkLongTranscripts = !options.NoChunk;
        beesum.ChunkTokens = options.ChunkTokens;
        beesum.LlmProviderId = config.ProviderId;

        if (options.YtDlpExtraArgs is not null)
        {
            foreach (var arg in options.YtDlpExtraArgs)
                beesum.YtDlpExtraArgs.Add(arg);
        }

        if (options.SubtitleLanguages is not null)
        {
            beesum.SubtitleLanguages.Clear();
            foreach (var language in options.SubtitleLanguages)
                beesum.SubtitleLanguages.Add(language);
        }

        switch (config.ProviderId)
        {
            case "openai-compatible":
                beesum.OllamaBaseUrl = null;
                beesum.OpenAiBaseUrl = config.BaseUrl;
                beesum.OpenAiModel = config.Model ?? "gpt-4o-mini";
                beesum.OpenAiApiKeyProvider = _ => Task.FromResult(config.ApiKey);
                break;

            case "anthropic":
                beesum.OllamaBaseUrl = null;
                beesum.AnthropicBaseUrl = config.BaseUrl;
                beesum.AnthropicModel = config.Model ?? "claude-sonnet-4-5";
                beesum.AnthropicApiKeyProvider = _ => Task.FromResult(config.ApiKey);
                break;

            case "gemini":
                beesum.OllamaBaseUrl = null;
                beesum.GeminiBaseUrl = config.BaseUrl;
                beesum.GeminiModel = config.Model ?? "gemini-2.5-flash";
                beesum.GeminiApiKeyProvider = _ => Task.FromResult(config.ApiKey);
                break;

            default:
                beesum.OllamaBaseUrl = options.OllamaBaseUrl;
                beesum.OllamaModel = options.OllamaModel;
                if (int.TryParse(Environment.GetEnvironmentVariable("BEESUM_OLLAMA_NUM_CTX"), out var numCtx) && numCtx > 0)
                    beesum.OllamaContextTokens = numCtx;
                break;
        }
    });

    await using var provider = services.BuildServiceProvider();
    var pipeline = provider.GetRequiredService<ISummarizerPipeline>();

    if (options.BatchFile is not null)
    {
        var urls = CliBatch.ReadUrls(options.BatchFile);
        if (urls.Count == 0)
            throw new ArgumentException($"Batch file has no URLs: {options.BatchFile}");

        var items = new List<BatchItem>(urls.Count);
        for (var index = 0; index < urls.Count; index++)
        {
            Console.Error.WriteLine($"[{index + 1}/{urls.Count}] {urls[index]}");

            var batchCommand = new SummarizeCommand(
                urls[index],
                options.Style,
                options.Format,
                Instructions: options.Instructions,
                SystemPrompt: systemPrompt,
                Timestamps: options.Timestamps,
                From: options.From,
                To: options.To);

            var batchResult = await pipeline.RunAsync(batchCommand, null, CancellationToken.None);
            if (batchResult.IsSuccess && batchResult.Truncated)
                Console.Error.WriteLine(TruncationWarning);
            items.Add(new BatchItem(urls[index], batchResult));
        }

        var output = options.Json
            ? JsonOutput.SerializeBatch(items, options.JsonLines)
            : CliBatch.FormatText(items);

        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            await File.WriteAllTextAsync(options.OutputPath, output);
            Console.Error.WriteLine($"Results written to {options.OutputPath}");
        }
        else
        {
            Console.WriteLine(output);
        }

        return items.All(item => item.Result.IsSuccess) ? 0 : 1;
    }

    Transcript? transcript = null;
    if (options.Stdin)
    {
        var text = await Console.In.ReadToEndAsync(CancellationToken.None);
        text = text.TrimStart('\uFEFF'); // some Windows pipes prepend a BOM
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("No transcript on stdin.");

        transcript = options.InputFormat == "vtt"
            ? provider.GetRequiredService<ISubtitleParser>().Parse(text, "stdin")
            : new Transcript("stdin", string.Empty, new[]
            {
                new SubtitleSegment(TimeSpan.Zero, TimeSpan.Zero, text),
            });
    }

    var command = new SummarizeCommand(
        options.Url,
        options.Style,
        options.Format,
        options.LocalVttPath,
        options.Instructions,
        systemPrompt,
        transcript,
        Timestamps: options.Timestamps,
        From: options.From,
        To: options.To,
        SaveVttPath: options.SaveVttPath,
        SaveTranscriptPath: options.SaveTranscriptPath);

    IProgress<PipelineStage>? progress = options.Json ? null : new ConsoleProgress();

    var result = await pipeline.RunAsync(command, progress, CancellationToken.None);

    if (result.IsSuccess && result.Truncated)
        Console.Error.WriteLine(TruncationWarning);

    if (result.IsSuccess && !string.IsNullOrWhiteSpace(options.SaveTranscriptPath))
        Console.Error.WriteLine($"Transcript written to {options.SaveTranscriptPath}");

    if (result.IsSuccess && !string.IsNullOrWhiteSpace(options.SaveVttPath))
        Console.Error.WriteLine($"Subtitles written to {Path.GetFullPath(options.SaveVttPath)}");

    if (options.Json)
    {
        var json = JsonOutput.Serialize(result);
        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            await File.WriteAllTextAsync(options.OutputPath, json);
            Console.Error.WriteLine($"JSON written to {options.OutputPath}");
        }
        else
        {
            Console.WriteLine(json);
        }

        return result.IsSuccess ? 0 : 1;
    }

    if (!result.IsSuccess)
    {
        Console.Error.WriteLine($"Error: {result.ErrorMessage}");
        return 1;
    }

    if (!string.IsNullOrEmpty(options.OutputPath))
    {
        await File.WriteAllTextAsync(options.OutputPath, result.Content);
        Console.Error.WriteLine($"Summary written to {options.OutputPath}");
    }
    else
    {
        Console.WriteLine(result.Content);
    }

    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(HelpText.Text);
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal: {ex.Message}");
    return 1;
}

internal sealed class ConsoleProgress : IProgress<PipelineStage>
{
    public void Report(PipelineStage value) =>
        Console.Error.WriteLine($"[{value}]");
}
