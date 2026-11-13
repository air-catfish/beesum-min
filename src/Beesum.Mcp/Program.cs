using System.Text;
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

// MCP messages are UTF-8 JSON. Use explicit streams instead of Console.In/Out, whose
// encoding on Windows follows the console code page (for example 866 on a Russian system).
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
using var stdin = new StreamReader(Console.OpenStandardInput(), utf8);
using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
await server.RunAsync(stdin, stdout, CancellationToken.None);
return 0;
