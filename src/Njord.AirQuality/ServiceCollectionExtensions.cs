using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;

namespace Njord.AirQuality;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAirQualityDomain(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, PollenAlertRule>();
        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, Pm25AlertRule>();
        services.AddSingleton<IDomainHandler>(static sp => new AirQualityAlertHandler(
            sp.GetServices<IAlertRule<DomainForecast<AirQualityPoint>>>(), sp.GetRequiredService<IOptions<AlertOptions>>(), "Pollen"));
        services.AddSingleton<IDomainHandler>(static sp => new AirQualityAlertHandler(
            sp.GetServices<IAlertRule<DomainForecast<AirQualityPoint>>>(), sp.GetRequiredService<IOptions<AlertOptions>>(), "Pm25"));
        services.AddSingleton<IDomainHandler, PollenTrendHandler>();
        return services;
    }
}
