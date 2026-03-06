using Archie.Core.Models;

namespace Archie.Core.Retrieval;

public interface IRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
