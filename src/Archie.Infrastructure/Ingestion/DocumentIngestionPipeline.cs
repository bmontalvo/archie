using Archie.Core.Ingestion;
using Archie.Core.Models;
using Archie.Core.Retrieval;

namespace Archie.Infrastructure.Ingestion;

public sealed class DocumentIngestionPipeline : IDocumentIngestionPipeline
{
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public DocumentIngestionPipeline(
        IDocumentChunker chunker,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore)
    {
        _chunker = chunker;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task<int> IngestDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        string[] files = Directory.GetFiles(directoryPath, "*.md", SearchOption.AllDirectories);
        int totalChunks = 0;

        foreach (string file in files)
        {
            IReadOnlyList<DocumentChunk> chunks = await _chunker.ChunkAsync(file, cancellationToken);

            List<DocumentChunk> embeddedChunks = new(chunks.Count);
            foreach (DocumentChunk chunk in chunks)
            {
                ReadOnlyMemory<float> embedding = await _embeddingService.EmbedAsync(chunk.Content, cancellationToken);
                embeddedChunks.Add(chunk with { Embedding = embedding });
            }

            await _vectorStore.UpsertAsync(embeddedChunks, cancellationToken);
            totalChunks += embeddedChunks.Count;
        }

        return totalChunks;
    }
}
