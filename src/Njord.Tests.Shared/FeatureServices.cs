using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.AirQuality;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Flood;
using Njord.Model;
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

    public static IReadOnlyList<IAlertRule<DomainForecast<WeatherPoint>>> WeatherRules(TimeProvider time) =>
        [.. Build(time, services => services
                .AddAlertOptions(AppSettingsDefaults.Configuration())
                .AddWeatherDomain())
            .GetServices<IAlertRule<DomainForecast<WeatherPoint>>>()];

    public static WeatherAlertHandler WeatherHandler(TimeProvider time, bool enabled = true) =>
        (WeatherAlertHandler)Build(time, services => services
                .AddAlertOptions(AppSettingsDefaults.Configuration())
                .AddWeatherDomain()
                .Configure<AlertOptions>(options => options.Enabled = enabled))
            .GetServices<IDomainHandler>().OfType<WeatherAlertHandler>().Single();

    public static IDomainHandler AlertHandler(TimeProvider time, Action<AlertOptions>? configure = null) =>
        Build(time, services =>
            {
                services.AddAlertOptions(AppSettingsDefaults.Configuration());
                services.AddWeatherDomain();
                services.AddAirQualityDomain();
                services.AddFloodDomain();
                if (configure is not null)
                {
                    services.Configure(configure);
                }
            })
            .GetServices<IDomainHandler>().First();
}
