using Archie.Core.Retrieval;
using Microsoft.Extensions.DependencyInjection;

namespace Archie.Infrastructure.VectorStore;

/// <summary>
/// Extension methods for registering vector store services.
/// </summary>
public static class VectorStoreServiceExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryVectorStore"/> as a singleton IVectorStore.
    /// Suitable for local development and testing without Azure AI Search.
    /// </summary>
    public static IServiceCollection AddInMemoryVectorStore(this IServiceCollection services)
    {
        services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        return services;
    }

    // TODO: AddAzureAISearchVectorStore(this IServiceCollection services, AzureAISearchOptions options)
}
