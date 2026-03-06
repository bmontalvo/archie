namespace Archie.Core.Models;

public sealed record QueryResponse(
    string Answer,
    IReadOnlyList<Citation> Citations,
    TokenUsage TokenUsage);
