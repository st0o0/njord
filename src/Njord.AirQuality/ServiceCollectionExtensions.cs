using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Core.Domain;

namespace Njord.AirQuality;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAirQualityAlerts(this IServiceCollection services)
    {
        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<AirQualityAlertInput>(
            new AirQualityAlertProjector(),
            [
                new AirQualityAlertEnrichment(
                    [new PollenAlertRule(), new Pm25AlertRule()],
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.ToSettings(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled),
            ]));
        return services;
    }
}
