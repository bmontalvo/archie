using Archie.Core.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace Archie.Infrastructure.Ingestion;

public static class IngestionServiceExtensions
{
    public static IServiceCollection AddDocumentIngestionPipeline(this IServiceCollection services)
    {
        services.AddTransient<IDocumentIngestionPipeline, DocumentIngestionPipeline>();
        return services;
    }
}
