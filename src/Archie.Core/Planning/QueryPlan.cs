namespace Archie.Core.Planning;

public sealed record QueryPlan(
    string OriginalQuestion,
    IReadOnlyList<string> SubQueries,
    bool RequiresDecomposition);
