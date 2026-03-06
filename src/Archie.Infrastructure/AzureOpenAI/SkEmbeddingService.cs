using Archie.Core.Ingestion;
using Microsoft.Extensions.AI;

namespace Archie.Infrastructure.AzureOpenAI;

/// <summary>
/// Implements IEmbeddingService by delegating to Microsoft.Extensions.AI's
/// IEmbeddingGenerator, which the Semantic Kernel Azure OpenAI connector registers.
/// </summary>
public sealed class SkEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _inner;

    public SkEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> inner)
    {
        _inner = inner;
    }

    public async Task<ReadOnlyMemory<float>> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        GeneratedEmbeddings<Embedding<float>> results = await _inner.GenerateAsync(
            [text],
            cancellationToken: cancellationToken);

        return results[0].Vector;
    }
}
