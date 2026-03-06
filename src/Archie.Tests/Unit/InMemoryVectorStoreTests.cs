using Archie.Core.Models;
using Archie.Infrastructure.VectorStore;
using FluentAssertions;

namespace Archie.Tests.Unit;

public sealed class InMemoryVectorStoreTests
{
    // All vectors are 2D unit vectors [cos(θ), sin(θ)].
    // Query vector is [1, 0] (θ = 0°).
    // Cosine similarity with query = cos(θ), so lower angle → higher score.
    // Chunks are assigned angles 0°, 10°, 20°, ..., 90° (10 chunks).
    // Expected top-3 by descending similarity: chunk 0 (0°), chunk 1 (10°), chunk 2 (20°).

    private const int ChunkCount = 10;
    private const double AngleStepDegrees = 10.0;

    private static ReadOnlyMemory<float> UnitVector(double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return new ReadOnlyMemory<float>([(float)Math.Cos(rad), (float)Math.Sin(rad)]);
    }

    private static IReadOnlyList<DocumentChunk> BuildChunks() =>
        Enumerable.Range(0, ChunkCount)
            .Select(i => new DocumentChunk(
                Content: $"Content for chunk {i}",
                SourceFile: "test.md",
                ChunkIndex: i,
                Embedding: UnitVector(i * AngleStepDegrees)))
            .ToList();

    [Fact]
    public async Task SearchAsync_WhenChunksUpserted_ShouldReturnTopKOrderedByDescendingScore()
    {
        InMemoryVectorStore store = new();
        await store.UpsertAsync(BuildChunks());

        ReadOnlyMemory<float> queryVector = UnitVector(0.0); // [1, 0]
        int topK = 3;

        IReadOnlyList<RetrievedChunk> results = await store.SearchAsync(queryVector, topK);

        results.Should().HaveCount(topK);
        // Chunks at 0°, 10°, 20° are the closest — in that order
        results[0].ChunkIndex.Should().Be(0);
        results[1].ChunkIndex.Should().Be(1);
        results[2].ChunkIndex.Should().Be(2);
        // Scores must be strictly descending
        results[0].Score.Should().BeGreaterThan(results[1].Score);
        results[1].Score.Should().BeGreaterThan(results[2].Score);
        // Perfect match at 0° has score ≈ 1.0
        results[0].Score.Should().BeApproximately(1.0, precision: 1e-5);
    }

    [Fact]
    public async Task SearchAsync_WhenTopKExceedsTotalChunks_ShouldReturnAllChunks()
    {
        InMemoryVectorStore store = new();
        await store.UpsertAsync(BuildChunks());

        IReadOnlyList<RetrievedChunk> results = await store.SearchAsync(UnitVector(0.0), topK: 100);

        results.Should().HaveCount(ChunkCount);
    }

    [Fact]
    public async Task SearchAsync_WhenStoreIsEmpty_ShouldReturnEmptyList()
    {
        InMemoryVectorStore store = new();

        IReadOnlyList<RetrievedChunk> results = await store.SearchAsync(UnitVector(0.0), topK: 5);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_WhenChunkUpsertedTwice_ShouldReplaceExistingEntry()
    {
        InMemoryVectorStore store = new();

        DocumentChunk original = new(
            Content: "original",
            SourceFile: "doc.md",
            ChunkIndex: 0,
            Embedding: UnitVector(90.0)); // orthogonal to query

        DocumentChunk updated = new(
            Content: "updated",
            SourceFile: "doc.md",
            ChunkIndex: 0,
            Embedding: UnitVector(0.0)); // perfect match to query

        await store.UpsertAsync([original]);
        await store.UpsertAsync([updated]);

        IReadOnlyList<RetrievedChunk> results = await store.SearchAsync(UnitVector(0.0), topK: 1);

        results.Should().HaveCount(1);
        results[0].Content.Should().Be("updated");
        results[0].Score.Should().BeApproximately(1.0, precision: 1e-5);
    }

    [Fact]
    public async Task SearchAsync_ShouldPopulateRetrievedChunkMetadataCorrectly()
    {
        InMemoryVectorStore store = new();
        DocumentChunk chunk = new(
            Content: "some content",
            SourceFile: "docs/readme.md",
            ChunkIndex: 7,
            Embedding: UnitVector(0.0));

        await store.UpsertAsync([chunk]);

        IReadOnlyList<RetrievedChunk> results = await store.SearchAsync(UnitVector(0.0), topK: 1);

        RetrievedChunk result = results[0];
        result.Content.Should().Be("some content");
        result.SourceDocument.Should().Be("docs/readme.md");
        result.ChunkIndex.Should().Be(7);
        result.Id.Should().Be("docs/readme.md:7");
    }

    [Theory]
    [InlineData(0.0, 1.0)]         // parallel → score = 1
    [InlineData(90.0, 0.0)]        // orthogonal → score = 0
    [InlineData(180.0, -1.0)]      // anti-parallel → score = -1
    public void CosineSimilarity_ShouldReturnExpectedScore(double angleDegrees, double expectedScore)
    {
        ReadOnlySpan<float> query = UnitVector(0.0).Span;
        ReadOnlySpan<float> candidate = UnitVector(angleDegrees).Span;

        double score = InMemoryVectorStore.CosineSimilarity(query, candidate);

        score.Should().BeApproximately(expectedScore, precision: 1e-5);
    }
}
