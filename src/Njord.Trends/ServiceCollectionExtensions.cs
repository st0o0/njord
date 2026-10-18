using Microsoft.Extensions.DependencyInjection;
using Njord.Consensus;

namespace Njord.Trends;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrends(this IServiceCollection services)
    {
        services.AddSingleton<TrendAnalyzer>();
        return services;
    }
}
