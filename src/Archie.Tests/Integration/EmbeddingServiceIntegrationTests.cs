using Archie.Core.Ingestion;
using Archie.Infrastructure.AzureOpenAI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Xunit.Abstractions;

namespace Archie.Tests.Integration;

/// <summary>
/// Integration tests for IEmbeddingService against a real Azure OpenAI endpoint.
///
/// Prerequisites — set these environment variables before running:
///   AZUREOPENAI__ENDPOINT             e.g. https://your-resource.openai.azure.com/
///   AZUREOPENAI__APIKEY               your Azure OpenAI API key
///   AZUREOPENAI__EMBEDDINGDEPLOYMENT  e.g. text-embedding-3-small
///
/// Run only integration tests:   dotnet test --filter "Category=Integration"
/// Exclude integration tests:    dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public sealed class EmbeddingServiceIntegrationTests(ITestOutputHelper output)
{
    private const int ExpectedDimensions = 1536;
    private const string SkipReason =
        "Skipped: set AZUREOPENAI__ENDPOINT, AZUREOPENAI__APIKEY, and AZUREOPENAI__EMBEDDINGDEPLOYMENT to run.";

    private AzureOpenAIOptions? TryLoadOptions()
    {
        string? endpoint = Environment.GetEnvironmentVariable("AZUREOPENAI__ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("AZUREOPENAI__APIKEY");
        string? embeddingDeployment = Environment.GetEnvironmentVariable("AZUREOPENAI__EMBEDDINGDEPLOYMENT");

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(embeddingDeployment))
        {
            output.WriteLine(SkipReason);
            return null;
        }

        // ChatDeployment is not needed for embedding tests; supply a placeholder.
        return new AzureOpenAIOptions(endpoint, apiKey, ChatDeployment: "unused", embeddingDeployment);
    }

    private static IEmbeddingService BuildEmbeddingService(AzureOpenAIOptions options)
    {
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureOpenAIServices(options);
        Kernel kernel = kernelBuilder.Build();
        return kernel.Services.GetRequiredService<IEmbeddingService>();
    }

    [Fact]
    public async Task EmbedAsync_WhenGivenAString_ShouldReturn1536DimensionVector()
    {
        AzureOpenAIOptions? options = TryLoadOptions();
        if (options is null) return;

        IEmbeddingService svc = BuildEmbeddingService(options);

        ReadOnlyMemory<float> embedding = await svc.EmbedAsync("The quick brown fox jumps over the lazy dog.");

        embedding.Length.Should().Be(ExpectedDimensions);
    }

    [Fact]
    public async Task EmbedAsync_WhenGivenDifferentStrings_ShouldReturnDifferentVectors()
    {
        AzureOpenAIOptions? options = TryLoadOptions();
        if (options is null) return;

        IEmbeddingService svc = BuildEmbeddingService(options);

        ReadOnlyMemory<float> embeddingA = await svc.EmbedAsync("cats");
        ReadOnlyMemory<float> embeddingB = await svc.EmbedAsync("astrophysics");

        embeddingA.ToArray().Should().NotEqual(embeddingB.ToArray());
    }

    [Fact]
    public async Task EmbedAsync_WhenGivenEmptyString_ShouldReturnVector()
    {
        AzureOpenAIOptions? options = TryLoadOptions();
        if (options is null) return;

        IEmbeddingService svc = BuildEmbeddingService(options);

        ReadOnlyMemory<float> embedding = await svc.EmbedAsync(string.Empty);

        embedding.Length.Should().Be(ExpectedDimensions);
    }
}
