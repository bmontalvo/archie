namespace Archie.Core.Ingestion;

public interface IDocumentIngestionPipeline
{
    /// <summary>
    /// Chunks, embeds, and stores all markdown files found in <paramref name="directoryPath"/>.
    /// Returns the total number of chunks ingested.
    /// </summary>
    Task<int> IngestDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}
