using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.AirQuality;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAirQualityDomain(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, PollenAlertRule>();
        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, Pm25AlertRule>();
        services.AddSingleton<IDomainHandler, AirQualityAlertHandler>();
        return services;
    }
}
