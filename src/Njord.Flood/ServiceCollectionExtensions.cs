using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Core.Domain;

namespace Njord.Flood;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFloodAlerts(this IServiceCollection services)
    {
        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<FloodRiskInput>(
            new FloodRiskProjector(),
            [
                new FloodRiskAlertEnrichment(
                    new FloodRiskAlertRule(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.ToSettings(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled),
            ]));

        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<FloodRainInput>(
            new FloodRainProjector(),
            [
                new FloodRainAlertEnrichment(
                    new FloodRainAlertRule(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.ToSettings(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled),
            ]));
        return services;
    }
}
