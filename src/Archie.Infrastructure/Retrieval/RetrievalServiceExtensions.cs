using Archie.Core.Retrieval;
using Microsoft.Extensions.DependencyInjection;

namespace Archie.Infrastructure.Retrieval;

public static class RetrievalServiceExtensions
{
    public static IServiceCollection AddBasicRetriever(this IServiceCollection services)
    {
        services.AddTransient<IRetriever, BasicRetriever>();
        return services;
    }
}
