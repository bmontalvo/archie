namespace Archie.Core.Models;

public sealed record DocumentChunk(
    string Content,
    string SourceFile,
    int ChunkIndex,
    ReadOnlyMemory<float> Embedding);
