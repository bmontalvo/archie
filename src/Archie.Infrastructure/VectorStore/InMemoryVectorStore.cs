using System.Collections.Concurrent;
using Archie.Core.Models;
using Archie.Core.Retrieval;

namespace Archie.Infrastructure.VectorStore;

public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, DocumentChunk> _store = new();

    public Task UpsertAsync(
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        foreach (DocumentChunk chunk in chunks)
            _store[ChunkId(chunk)] = chunk;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RetrievedChunk> results = [.. _store.Values
            .Select(chunk => new RetrievedChunk(
                Id: ChunkId(chunk),
                Content: chunk.Content,
                SourceDocument: chunk.SourceFile,
                ChunkIndex: chunk.ChunkIndex,
                Score: CosineSimilarity(queryVector.Span, chunk.Embedding.Span)))
            .OrderByDescending(r => r.Score)
            .Take(topK)];

        return Task.FromResult(results);
    }

    private static string ChunkId(DocumentChunk chunk) => $"{chunk.SourceFile}:{chunk.ChunkIndex}";

    internal static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length.");

        double dot = 0d;
        double normA = 0d;
        double normB = 0d;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0d || normB == 0d)
            return 0d;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
