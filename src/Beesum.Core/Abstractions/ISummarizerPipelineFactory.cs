namespace Beesum.Core.Abstractions;

/// <summary>
/// Creates <see cref="ISummarizerPipeline"/> instances for a specific provider id.
/// </summary>
/// <remarks>
/// Use this when the provider is chosen at run time (for example, a UI dropdown)
/// instead of being fixed in <see cref="Core.BeesumOptions.LlmProviderId"/>.
/// </remarks>
public interface ISummarizerPipelineFactory
{
    /// <summary>Creates a pipeline that uses the provider registered under <paramref name="providerId"/>.</summary>
    /// <param name="providerId">Provider id, e.g. <c>transcript-only</c> or <c>ollama</c>.</param>
    /// <returns>A new pipeline instance.</returns>
    ISummarizerPipeline Create(string providerId);
}
