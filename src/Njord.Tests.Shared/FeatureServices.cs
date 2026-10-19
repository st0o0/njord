using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.AirQuality;
using Njord.Alerts;
using Njord.Alerts.Configuration;
using Njord.Core.Domain;
using Njord.Flood;
using Njord.Weather.Alerts;

namespace Njord.Tests.Shared;

public static class FeatureServices
{
    public static ServiceProvider Build(TimeProvider time, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(AppSettingsDefaults.Configuration());
        services.AddSingleton(time);
        configure(services);
        return services.BuildServiceProvider();
    }

    public static AlertAnalyzer Alerts(TimeProvider time, bool enabled = true) =>
        Build(time, services => services
                .AddAlertOptions(AppSettingsDefaults.Configuration())
                .AddWeatherAlerts()
                .Configure<AlertOptions>(options => options.Enabled = enabled))
            .GetRequiredService<AlertAnalyzer>();

    public static IProjectionGroup AlertGroup(TimeProvider time, Action<AlertOptions>? configure = null) =>
        Build(time, services =>
            {
                services.AddAlertOptions(AppSettingsDefaults.Configuration());
                services.AddWeatherAlerts();
                services.AddAirQualityAlerts();
                services.AddFloodAlerts();
                if (configure is not null)
                {
                    services.Configure(configure);
                }
            })
            .GetServices<IProjectionGroup>().First();
}
