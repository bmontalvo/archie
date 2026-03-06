using Archie.Core.Models;

namespace Archie.Core.Synthesis;

public interface ISynthesizer
{
    Task<QueryResponse> SynthesizeAsync(
        QueryRequest request,
        IReadOnlyList<RetrievedChunk> chunks,
        CancellationToken cancellationToken = default);
}
