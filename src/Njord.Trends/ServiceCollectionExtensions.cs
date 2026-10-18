using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Configuration;
using Njord.Trends.Configuration;

namespace Njord.Trends;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrends(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<TrendOptions>(configuration);

        services.AddSingleton<TrendAnalyzer>();
        return services;
    }
}
