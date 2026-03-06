using Archie.Core.Models;

namespace Archie.Core.Ingestion;

public interface IDocumentChunker
{
    Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
