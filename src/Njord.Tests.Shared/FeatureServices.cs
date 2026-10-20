using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Advisories;
using Njord.Alerts;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Insights;
using Njord.Scores;

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
                .AddAlertHandlers())
            .GetServices<IAlertRule<DomainForecast<WeatherPoint>>>()];

    public static WeatherAlertHandler WeatherHandler(TimeProvider time, bool enabled = true, Action<FeatureOptions>? features = null) =>
        (WeatherAlertHandler)Build(time, services => services
                .AddAlertOptions(AppSettingsDefaults.Configuration())
                .AddAlertHandlers()
                .Configure<AlertOptions>(options => options.Enabled = enabled)
                .Configure<FeatureOptions>(options => features?.Invoke(options)))
            .GetServices<IDomainHandler>().OfType<WeatherAlertHandler>().Single();

    public static IDomainHandler AlertHandler(TimeProvider time, Action<AlertOptions>? configure = null) =>
        Build(time, services =>
            {
                services.AddAlertOptions(AppSettingsDefaults.Configuration());
                services.AddAlertHandlers();
                services.AddScoreHandlers();
                services.AddInsightHandlers();
                services.AddAdvisoryHandlers();
                if (configure is not null)
                {
                    services.Configure(configure);
                }
            })
            .GetServices<IDomainHandler>().First();
}
