using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Alerts.Configuration;
using Njord.Alerts.Rules;
using Njord.Core.Configuration;

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
        return services;
    }
}
