using Archie.Core.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace Archie.Infrastructure.DocumentParsing;

/// <summary>
/// Extension methods for registering document parsing services (PDF, Markdown).
/// </summary>
public static class DocumentParsingServiceExtensions
{
    public static IServiceCollection AddDocumentParsing(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentChunker, MarkdownDocumentChunker>();
        return services;
    }
}
