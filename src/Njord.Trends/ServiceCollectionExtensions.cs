using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration.Trends;

namespace Njord.Trends;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrends(this IServiceCollection services)
    {
        services
            .AddOptions<TrendOptions>()
            .BindConfiguration(TrendOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TrendOptions>, TrendOptionsValidator>();

        services.AddSingleton<TrendComputer>();
        services.AddSingleton<TrendAnalyzer>();
        return services;
    }
}
