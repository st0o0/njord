using Microsoft.Extensions.DependencyInjection;
using Njord.Alerts.Rules;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlertHandlers(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, FrostRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, HeatRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, StormRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, HeavyRainRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, UvRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, FogRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, SnowRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, PressureDropRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, ThunderstormRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, IceRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, WindChillRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, VisibilityRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, TropicalNightRule>();
        services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, HumidityRule>();

        services.AddSingleton<IDomainHandler, WeatherAlertHandler>();

        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, PollenAlertRule>();
        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, Pm25AlertRule>();
        services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, DustAlertRule>();
        services.AddSingleton<IDomainHandler, AirQualityAlertHandler>();

        services.AddSingleton<IAlertRule<DomainForecast<FloodPoint>>, FloodRiskAlertRule>();
        services.AddSingleton<IAlertRule<FloodRainInput>, FloodRainAlertRule>();
        services.AddSingleton<IDomainHandler, FloodRiskHandler>();
        services.AddSingleton<IDomainHandler, FloodRainHandler>();

        services.AddSingleton<IAlertRule<DomainForecast<MarinePoint>>, SwimmingConditionsRule>();
        services.AddSingleton<IDomainHandler, SwimmingConditionsHandler>();

        return services;
    }
}
