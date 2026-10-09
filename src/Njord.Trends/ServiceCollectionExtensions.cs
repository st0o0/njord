using Microsoft.Extensions.DependencyInjection;
using Njord.Compute.Analysis;

namespace Njord.Trends;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrends(this IServiceCollection services)
    {
        services.AddSingleton<TrendComputer>();
        services.AddSingleton<TrendAnalyzer>();
        return services;
    }
}
