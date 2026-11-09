using System.Text.Json;
using Beesum.Core.Models;
using Xunit;

namespace Beesum.Min.Cli.Tests;

public class JsonOutputTests
{
    [Fact]
    public void Serialize_Success_IncludesMetadata()
    {
        var result = new SummaryResult(
            true,
            "Hello",
            "ollama",
            ModelName: "llama3.2",
            Usage: new TokenUsage(1, 2, 3),
            Duration: TimeSpan.FromMilliseconds(12.5));

        var json = JsonOutput.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("Hello", root.GetProperty("content").GetString());
        Assert.Equal("ollama", root.GetProperty("providerId").GetString());
        Assert.Equal("llama3.2", root.GetProperty("modelName").GetString());
        Assert.Equal(3, root.GetProperty("usage").GetProperty("totalTokens").GetInt32());
        Assert.Equal(12.5, root.GetProperty("durationMs").GetDouble(), 3);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errorMessage").ValueKind);
    }

    [Fact]
    public void Serialize_MatchesGoldenShape()
    {
        var result = new SummaryResult(
            true,
            "Hello",
            "ollama",
            ModelName: "llama3.2",
            Usage: new TokenUsage(1, 2, 3),
            Duration: TimeSpan.FromMilliseconds(12.5));

        var json = JsonOutput.Serialize(result).ReplaceLineEndings("\n");

        var expected = string.Join('\n', new[]
        {
            "{",
            "  \"isSuccess\": true,",
            "  \"content\": \"Hello\",",
            "  \"providerId\": \"ollama\",",
            "  \"errorMessage\": null,",
            "  \"modelName\": \"llama3.2\",",
            "  \"usage\": {",
            "    \"promptTokens\": 1,",
            "    \"completionTokens\": 2,",
            "    \"totalTokens\": 3",
            "  },",
            "  \"durationMs\": 12.5",
            "}",
        });

        Assert.Equal(expected, json);
    }    [Fact]
    public void Serialize_Failure_IncludesErrorAndNullMetadata()
    {
        var result = new SummaryResult(false, string.Empty, "ollama", "HTTP 500");

        var json = JsonOutput.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("HTTP 500", root.GetProperty("errorMessage").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("usage").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("durationMs").ValueKind);
    }
    [Fact]
    public void SerializeBatch_MatchesGoldenArray()
    {
        var items = BatchItems();
        var json = JsonOutput.SerializeBatch(items, lines: false).ReplaceLineEndings("\n");

        var expected = string.Join('\n', new[]
        {
            "[",
            "  {",
            "    \"url\": \"https://a\",",
            "    \"isSuccess\": true,",
            "    \"content\": \"Hello\",",
            "    \"providerId\": \"ollama\",",
            "    \"errorMessage\": null,",
            "    \"modelName\": \"llama3.2\",",
            "    \"usage\": {",
            "      \"promptTokens\": 1,",
            "      \"completionTokens\": 2,",
            "      \"totalTokens\": 3",
            "    },",
            "    \"durationMs\": 12.5",
            "  },",
            "  {",
            "    \"url\": \"https://b\",",
            "    \"isSuccess\": false,",
            "    \"content\": \"\",",
            "    \"providerId\": null,",
            "    \"errorMessage\": \"nope\",",
            "    \"modelName\": null,",
            "    \"usage\": null,",
            "    \"durationMs\": null",
            "  }",
            "]",
        });

        Assert.Equal(expected, json);
    }

    [Fact]
    public void SerializeBatch_LinesMode_IsOneCompactObjectPerLine()
    {
        var lines = JsonOutput.SerializeBatch(BatchItems(), lines: true)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("{\"url\":\"https://a\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"isSuccess\":true", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("{\"url\":\"https://b\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"isSuccess\":false", lines[1], StringComparison.Ordinal);
    }

    private static BatchItem[] BatchItems() =>
        new[]
        {
            new BatchItem(
                "https://a",
                new SummaryResult(
                    true,
                    "Hello",
                    "ollama",
                    ModelName: "llama3.2",
                    Usage: new TokenUsage(1, 2, 3),
                    Duration: TimeSpan.FromMilliseconds(12.5))),
            new BatchItem(
                "https://b",
                new SummaryResult(false, string.Empty, null, "nope")),
        };}
