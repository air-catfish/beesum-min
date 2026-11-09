using Beesum.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Beesum.Testing;

/// <summary>DI helpers for tests.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the storage, yt-dlp, parser, and LLM registrations added by
    /// <c>AddBeesum</c> with in-memory implementations.
    /// </summary>
    /// <param name="services">Service collection, already configured with <c>AddBeesum</c>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// A single <see cref="InMemoryLlmProvider"/> with id <c>transcript-only</c> is
    /// registered. Add more providers (or replace this one) after calling this
    /// method when the pipeline needs a different id.
    /// </remarks>
    public static IServiceCollection AddBeesumTesting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ISettingsStore>();
        services.RemoveAll<ISecretStore>();
        services.RemoveAll<ITranscriptCache>();
        services.RemoveAll<ISummaryHistory>();
        services.RemoveAll<ISubtitleParser>();
        services.RemoveAll<IProcessRunner>();
        services.RemoveAll<IYtDlpPathResolver>();
        services.RemoveAll<IYtDlpService>();
        services.RemoveAll<ILlmProvider>();

        services.AddSingleton<ISettingsStore, InMemorySettingsStore>();
        services.AddSingleton<ISecretStore, InMemorySecretStore>();
        services.AddSingleton<ITranscriptCache, InMemoryTranscriptCache>();
        services.AddSingleton<ISummaryHistory, InMemorySummaryHistory>();
        services.AddSingleton<ISubtitleParser, InMemorySubtitleParser>();
        services.AddSingleton<IProcessRunner, InMemoryProcessRunner>();
        services.AddSingleton<IYtDlpPathResolver, InMemoryYtDlpPathResolver>();
        services.AddSingleton<IYtDlpService, InMemoryYtDlpService>();
        services.AddSingleton<ILlmProvider, InMemoryLlmProvider>();

        return services;
    }
}
