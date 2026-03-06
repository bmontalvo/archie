using Archie.Core.Ingestion;
using Archie.Core.Models;
using Archie.Core.Retrieval;
using Archie.Infrastructure.Retrieval;
using FluentAssertions;

namespace Archie.Tests.Unit;

public sealed class BasicRetrieverTests
{
    // ---------------------------------------------------------------------------
    // Stubs
    // ---------------------------------------------------------------------------

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        private readonly ReadOnlyMemory<float> _vector;
        public string? LastText { get; private set; }

        public StubEmbeddingService(ReadOnlyMemory<float> vector) => _vector = vector;

        public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            LastText = text;
            return Task.FromResult(_vector);
        }
    }

    private sealed class StubVectorStore : IVectorStore
    {
        private readonly IReadOnlyList<RetrievedChunk> _results;
        public ReadOnlyMemory<float> LastQueryVector { get; private set; }
        public int LastTopK { get; private set; }

        public StubVectorStore(IReadOnlyList<RetrievedChunk> results) => _results = results;

        public Task UpsertAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            ReadOnlyMemory<float> queryVector,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            LastQueryVector = queryVector;
            LastTopK = topK;
            return Task.FromResult(_results);
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ReadOnlyMemory<float> Vector(params float[] values) => new(values);

    private static RetrievedChunk MakeChunk(int index, double score) =>
        new(Id: $"doc.md:{index}", Content: $"chunk {index}", SourceDocument: "doc.md", ChunkIndex: index, Score: score);

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RetrieveAsync_ShouldEmbedQueryAndForwardVectorToVectorStore()
    {
        ReadOnlyMemory<float> expectedVector = Vector(0.5f, 0.5f, 0.707f);
        StubEmbeddingService embeddingService = new(expectedVector);
        StubVectorStore vectorStore = new([]);
        BasicRetriever retriever = new(embeddingService, vectorStore);

        await retriever.RetrieveAsync("what is RAG?", topK: 5);

        embeddingService.LastText.Should().Be("what is RAG?");
        vectorStore.LastQueryVector.ToArray().Should().BeEquivalentTo(expectedVector.ToArray());
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPassTopKToVectorStore()
    {
        StubEmbeddingService embeddingService = new(Vector(1f, 0f));
        StubVectorStore vectorStore = new([]);
        BasicRetriever retriever = new(embeddingService, vectorStore);

        await retriever.RetrieveAsync("query", topK: 7);

        vectorStore.LastTopK.Should().Be(7);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldReturnChunksFromVectorStore()
    {
        IReadOnlyList<RetrievedChunk> expected = [MakeChunk(0, 0.99), MakeChunk(1, 0.85), MakeChunk(2, 0.70)];
        StubEmbeddingService embeddingService = new(Vector(1f, 0f));
        StubVectorStore vectorStore = new(expected);
        BasicRetriever retriever = new(embeddingService, vectorStore);

        IReadOnlyList<RetrievedChunk> results = await retriever.RetrieveAsync("query", topK: 3);

        results.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task RetrieveAsync_WhenVectorStoreReturnsEmpty_ShouldReturnEmptyList()
    {
        StubEmbeddingService embeddingService = new(Vector(1f, 0f));
        StubVectorStore vectorStore = new([]);
        BasicRetriever retriever = new(embeddingService, vectorStore);

        IReadOnlyList<RetrievedChunk> results = await retriever.RetrieveAsync("query");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_ShouldPassCancellationTokenThrough()
    {
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;
        bool tokenReachedEmbedding = false;
        bool tokenReachedStore = false;

        CapturingEmbeddingService embeddingService = new(Vector(1f, 0f), t => tokenReachedEmbedding = t == token);
        CapturingVectorStore vectorStore = new([], t => tokenReachedStore = t == token);
        BasicRetriever retriever = new(embeddingService, vectorStore);

        await retriever.RetrieveAsync("query", cancellationToken: token);

        tokenReachedEmbedding.Should().BeTrue();
        tokenReachedStore.Should().BeTrue();
    }

    private sealed class CapturingEmbeddingService(ReadOnlyMemory<float> vector, Action<CancellationToken> capture) : IEmbeddingService
    {
        public Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            capture(cancellationToken);
            return Task.FromResult(vector);
        }
    }

    private sealed class CapturingVectorStore(IReadOnlyList<RetrievedChunk> results, Action<CancellationToken> capture) : IVectorStore
    {
        public Task UpsertAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
            ReadOnlyMemory<float> queryVector,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            capture(cancellationToken);
            return Task.FromResult(results);
        }
    }
}
