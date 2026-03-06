using Archie.Core.Ingestion;
using Archie.Core.Models;
using Archie.Core.Retrieval;

namespace Archie.Infrastructure.Retrieval;

public sealed class BasicRetriever : IRetriever
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public BasicRetriever(IEmbeddingService embeddingService, IVectorStore vectorStore)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<float> queryVector = await _embeddingService.EmbedAsync(query, cancellationToken);
        return await _vectorStore.SearchAsync(queryVector, topK, cancellationToken);
    }
}
