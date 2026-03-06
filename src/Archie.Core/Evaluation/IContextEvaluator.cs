using Archie.Core.Models;

namespace Archie.Core.Evaluation;

public interface IContextEvaluator
{
    Task<ContextEvaluation> EvaluateAsync(
        string question,
        IReadOnlyList<RetrievedChunk> chunks,
        CancellationToken cancellationToken = default);
}
