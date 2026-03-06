using Archie.Core.Synthesis;
using Microsoft.Extensions.DependencyInjection;

namespace Archie.Infrastructure.Synthesis;

public static class SynthesisServiceExtensions
{
    public static IServiceCollection AddBasicSynthesizer(this IServiceCollection services)
    {
        services.AddTransient<ISynthesizer, BasicSynthesizer>();
        return services;
    }
}
