namespace Archie.Core.Models;

public sealed record RetrievedChunk(
    string Id,
    string Content,
    string SourceDocument,
    int ChunkIndex,
    double Score);
