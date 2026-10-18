using Microsoft.Extensions.DependencyInjection;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;

namespace Njord.Trends;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrends(this IServiceCollection services)
    {
        services.AddSingleton<TrendAnalyzer>();
        return services;
    }
}
