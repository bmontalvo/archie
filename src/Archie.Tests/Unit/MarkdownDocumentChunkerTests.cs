using Archie.Infrastructure.DocumentParsing;
using FluentAssertions;

namespace Archie.Tests.Unit;

public sealed class MarkdownDocumentChunkerTests
{
    // Chunk constants (mirrored here to keep assertions readable)
    private const int ChunkSize = 512;
    private const int OverlapSize = 50;
    private const int Stride = ChunkSize - OverlapSize; // 462

    private static string MakeTokens(int count, string prefix = "word") =>
        string.Join(" ", Enumerable.Range(0, count).Select(i => $"{prefix}{i}"));

    // -------------------------------------------------------------------------
    // Chunk count
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk_WhenDocumentFitsInSingleChunk_ShouldReturnOneChunk()
    {
        string content = MakeTokens(ChunkSize); // exactly 512 tokens

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        chunks.Should().HaveCount(1);
    }

    [Fact]
    public void Chunk_WhenDocumentExceedsSingleChunk_ShouldReturnExpectedChunkCount()
    {
        // 1000 tokens → starts at 0, 462, 924 → 3 chunks
        string content = MakeTokens(1000);

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        chunks.Should().HaveCount(3);
    }

    [Fact]
    public void Chunk_WhenDocumentHasExactlyStrideTokens_ShouldReturnOneChunk()
    {
        // Stride (462) tokens — fits in one chunk, no second pass needed
        string content = MakeTokens(Stride);

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        chunks.Should().HaveCount(1);
    }

    [Fact]
    public void Chunk_WhenDocumentIsEmpty_ShouldReturnEmptyList()
    {
        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk("", "test.md");

        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Chunk_WhenDocumentIsWhitespaceOnly_ShouldReturnEmptyList()
    {
        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk("   \n\t  ", "test.md");

        chunks.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Overlap
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk_WhenDocumentSpansTwoChunks_ShouldHaveOverlapBetweenConsecutiveChunks()
    {
        // 600 tokens → chunk 0: [0..512], chunk 1: [462..600]
        string content = MakeTokens(600);
        string[] allTokens = content.Split(' ');

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        chunks.Should().HaveCount(2);

        string[] chunk0Tokens = chunks[0].Content.Split(' ');
        string[] chunk1Tokens = chunks[1].Content.Split(' ');

        // The last OverlapSize tokens of chunk 0 must equal the first OverlapSize tokens of chunk 1
        string[] tailOfChunk0 = chunk0Tokens[^OverlapSize..];
        string[] headOfChunk1 = chunk1Tokens[..OverlapSize];

        tailOfChunk0.Should().Equal(headOfChunk1);
    }

    [Fact]
    public void Chunk_WhenDocumentSpansThreeChunks_ShouldHaveOverlapBetweenAllConsecutivePairs()
    {
        string content = MakeTokens(1000);

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        chunks.Should().HaveCount(3);

        for (int i = 0; i < chunks.Count - 1; i++)
        {
            string[] currentTokens = chunks[i].Content.Split(' ');
            string[] nextTokens = chunks[i + 1].Content.Split(' ');

            // Tail of current chunk must equal head of next chunk (overlap region)
            string[] tail = currentTokens[^OverlapSize..];
            string[] head = nextTokens[..OverlapSize];

            tail.Should().Equal(head, because: $"chunk {i} and chunk {i + 1} should share a {OverlapSize}-token overlap");
        }
    }

    // -------------------------------------------------------------------------
    // Chunk content size
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk_WhenDocumentExceedsSingleChunk_ShouldCapEachChunkAtChunkSize()
    {
        string content = MakeTokens(1000);

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        foreach (Core.Models.DocumentChunk chunk in chunks)
        {
            chunk.Content.Split(' ').Length.Should().BeLessThanOrEqualTo(ChunkSize);
        }
    }

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Chunk_WhenGivenSourceFile_ShouldPreserveSourceFileOnAllChunks()
    {
        const string sourceFile = "/docs/my-document.md";
        string content = MakeTokens(1000);

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, sourceFile);

        chunks.Should().AllSatisfy(c => c.SourceFile.Should().Be(sourceFile));
    }

    [Fact]
    public void Chunk_WhenProducingMultipleChunks_ShouldAssignSequentialChunkIndexes()
    {
        string content = MakeTokens(1000);

        IReadOnlyList<Core.Models.DocumentChunk> chunks = MarkdownDocumentChunker.Chunk(content, "test.md");

        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].ChunkIndex.Should().Be(i);
        }
    }
}
