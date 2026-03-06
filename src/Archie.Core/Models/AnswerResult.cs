namespace Archie.Core.Models;

public sealed record AnswerResult(
    string Answer,
    IReadOnlyList<string> SourceIds,
    double Confidence,
    IReadOnlyList<string> InformationGaps);
