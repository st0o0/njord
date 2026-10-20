using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Flood;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFloodDomain(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRule<DomainForecast<FloodPoint>>, FloodRiskAlertRule>();
        services.AddSingleton<IAlertRule<FloodRainInput>, FloodRainAlertRule>();
        services.AddSingleton<IDomainHandler, FloodRiskHandler>();
        services.AddSingleton<IDomainHandler, FloodRainHandler>();
        services.AddSingleton<IDomainHandler, FloodTimelineHandler>();
        return services;
    }
}
