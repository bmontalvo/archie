using Archie.Core.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Archie.Infrastructure.AzureOpenAI;

/// <summary>
/// Extension methods for registering Azure OpenAI services.
/// </summary>
public static class AzureOpenAIServiceExtensions
{
    /// <summary>
    /// Registers Azure OpenAI LLM and embedding services with Semantic Kernel.
    /// Call this on the IKernelBuilder returned by services.AddKernel().
    /// </summary>
    public static IKernelBuilder AddAzureOpenAIServices(
        this IKernelBuilder kernelBuilder,
        AzureOpenAIOptions options)
    {
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: options.ChatDeployment,
            endpoint: options.Endpoint,
            apiKey: options.ApiKey);

        // SKEXP0010: SK deprecated ITextEmbeddingGenerationService in favour of the MEAI
        // IEmbeddingGenerator API, but AddAzureOpenAIEmbeddingGenerator is still marked
        // experimental. Suppress until SK stabilises this migration path.
#pragma warning disable SKEXP0010
        kernelBuilder.AddAzureOpenAIEmbeddingGenerator(
            deploymentName: options.EmbeddingDeployment,
            endpoint: options.Endpoint,
            apiKey: options.ApiKey);
#pragma warning restore SKEXP0010

        kernelBuilder.Services.AddSingleton<IEmbeddingService, SkEmbeddingService>();

        return kernelBuilder;
    }
}
