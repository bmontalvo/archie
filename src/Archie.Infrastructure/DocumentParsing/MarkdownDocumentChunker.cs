using Archie.Core.Ingestion;
using Archie.Core.Models;

namespace Archie.Infrastructure.DocumentParsing;

/// <summary>
/// Chunks a markdown file into fixed-size token windows with overlap.
/// Token count is estimated by whitespace splitting — replace with a proper tokenizer in a later phase.
/// </summary>
public sealed class MarkdownDocumentChunker : IDocumentChunker
{
    private const int ChunkSize = 512;
    private const int OverlapSize = 50;
    private const int Stride = ChunkSize - OverlapSize; // 462

    public async Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Chunk(content, filePath);
    }

    // Internal helper exposed as internal for unit testing without file I/O.
    internal static IReadOnlyList<DocumentChunk> Chunk(string content, string sourceFile)
    {
        string[] tokens = content.Split(
            [' ', '\t', '\n', '\r', '\f', '\v'],
            StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
            return [];

        List<DocumentChunk> chunks = [];
        int chunkIndex = 0;

        for (int start = 0; start < tokens.Length; start += Stride)
        {
            int end = Math.Min(start + ChunkSize, tokens.Length);
            string chunkContent = string.Join(" ", tokens[start..end]);

            chunks.Add(new DocumentChunk(
                Content: chunkContent,
                SourceFile: sourceFile,
                ChunkIndex: chunkIndex++,
                Embedding: ReadOnlyMemory<float>.Empty));

            if (end == tokens.Length)
                break;
        }

        return chunks;
    }
}
