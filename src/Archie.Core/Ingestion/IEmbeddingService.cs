namespace Archie.Core.Ingestion;

public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);
}
