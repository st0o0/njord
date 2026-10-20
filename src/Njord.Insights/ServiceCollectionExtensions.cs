using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;

namespace Njord.Insights;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInsightHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IDomainHandler, SeasonalOutlookHandler>();
        services.AddSingleton<IDomainHandler, SolarYieldHandler>();
        services.AddSingleton<IDomainHandler, SatelliteRadiationHandler>();
        services.AddSingleton<IDomainHandler, ConfidenceTrendHandler>();
        services.AddSingleton<IDomainHandler, ApproachingFrontHandler>();
        services.AddSingleton<IDomainHandler, PollenTrendHandler>();
        return services;
    }
}
