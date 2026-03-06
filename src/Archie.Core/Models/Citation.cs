namespace Archie.Core.Models;

public sealed record Citation(
    string SourceFile,
    int ChunkIndex,
    string Snippet);
