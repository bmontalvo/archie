using Archie.Core.Models;

namespace Archie.Core.Synthesis;

public interface ISynthesisEngine
{
    Task<AnswerResult> SynthesizeAsync(
        string question,
        IReadOnlyList<RetrievedChunk> chunks,
        CancellationToken cancellationToken = default);
}
