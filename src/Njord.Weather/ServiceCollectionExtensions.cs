using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Weather.Alerts.Rules;

namespace Njord.Weather.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWeatherAlerts(this IServiceCollection services)
    {
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
        services.AddSingleton<IDomainHandler>(static sp => new WeatherAlertHandler(
            sp.GetRequiredService<AlertAnalyzer>(),
            sp.GetRequiredService<IOptions<AlertOptions>>().Value.ToSettings()));

        return services;
    }
}
