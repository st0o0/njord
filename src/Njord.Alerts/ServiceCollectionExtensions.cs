using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Alerts.AirQuality;
using Njord.Alerts.Configuration;
using Njord.Alerts.Flood;
using Njord.Alerts.Rules;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;

namespace Njord.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlerts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNjordOptions<AlertOptions>(configuration);

        services.AddSingleton<IAlertRule, FrostRule>();
        services.AddSingleton<IAlertRule, HeatRule>();
        services.AddSingleton<IAlertRule, StormRule>();
        services.AddSingleton<IAlertRule, HeavyRainRule>();
        services.AddSingleton<IAlertRule, UvRule>();
        services.AddSingleton<IAlertRule, FogRule>();
        services.AddSingleton<IAlertRule, SnowRule>();
        services.AddSingleton<IAlertRule, PressureDropRule>();
        services.AddSingleton<IAlertRule, ThunderstormRule>();
        services.AddSingleton<IAlertRule, IceRule>();
        services.AddSingleton<IAlertRule, WindChillRule>();
        services.AddSingleton<IAlertRule, VisibilityRule>();
        services.AddSingleton<IAlertRule, TropicalNightRule>();
        services.AddSingleton<IAlertRule, HumidityRule>();

        services.AddSingleton<AlertAnalyzer>();
        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<WeatherAlertInput>(
            new WeatherAlertProjector(),
            [
                new WeatherAlertEnrichment(
                    sp.GetRequiredService<AlertAnalyzer>(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.ToSettings()),
            ]));

        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<AirQualityAlertInput>(
            new AirQualityAlertProjector(),
            [
                new AirQualityAlertEnrichment(
                    [new PollenAlertRule(), new Pm25AlertRule()],
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled),
            ]));

        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<FloodRiskInput>(
            new FloodRiskProjector(),
            [
                new FloodRiskAlertEnrichment(
                    new FloodRiskAlertRule(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled),
            ]));

        services.AddSingleton<IProjectionGroup>(static sp => new ProjectionGroup<FloodRainInput>(
            new FloodRainProjector(),
            [
                new FloodRainAlertEnrichment(
                    new FloodRainAlertRule(),
                    sp.GetRequiredService<IOptions<AlertOptions>>().Value.Enabled),
            ]));
        return services;
    }
}
