using Microsoft.Extensions.DependencyInjection;
using Njord.Compute.Analysis;

namespace Njord.Indices;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIndices(this IServiceCollection services)
    {
        services.AddSingleton<IndexComputer>();
        services.AddSingleton<IndexAnalyzer>();
        return services;
    }
}
