using Archie.Core.Models;

namespace Archie.Core.Retrieval;

public interface IVectorStore
{
    Task UpsertAsync(
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
