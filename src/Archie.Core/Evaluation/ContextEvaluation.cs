namespace Archie.Core.Evaluation;

public sealed record ContextEvaluation(
    bool IsSufficient,
    string? ReformulatedQuery,
    string Reasoning);
