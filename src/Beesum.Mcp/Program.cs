using Beesum.Core.Abstractions;
using Beesum.Core.DependencyInjection;
using Beesum.Mcp;
using Microsoft.Extensions.DependencyInjection;

if (args.Contains("--version", StringComparer.Ordinal))
{
    Console.WriteLine($"beesum-mcp {McpServer.Version}");
    return 0;
}

var services = new ServiceCollection();
services.AddBeesum(McpOptions.Configure);

await using var provider = services.BuildServiceProvider();
var server = new McpServer(
    provider.GetRequiredService<ISummarizerPipelineFactory>(),
    provider.GetRequiredService<ISubtitleParser>(),
    McpOptions.DefaultProvider);

Console.Error.WriteLine($"beesum-mcp {McpServer.Version} ready on stdio");
await server.RunAsync(Console.In, Console.Out, CancellationToken.None);
return 0;
