using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Domain;
using Njord.Model;
using Njord.Weather.Alerts.Rules;
using Njord.Weather.Lifestyle;

namespace Njord.Weather.Alerts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWeatherDomain(this IServiceCollection services)
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
        services.AddSingleton<IDomainHandler, VentilationWindowHandler>();
        services.AddSingleton<IDomainHandler, ExerciseAdvisoryHandler>();
        services.AddSingleton<IDomainHandler, LaundryScoreHandler>();
        services.AddSingleton<IDomainHandler, CommuteSafetyHandler>();
        services.AddSingleton<IDomainHandler, NightVentilationHandler>();
        services.AddSingleton<IDomainHandler, ConfidenceTrendHandler>();
        services.AddSingleton<IDomainHandler, SolarYieldHandler>();
        services.AddSingleton<IDomainHandler, GardeningHandler>();
        services.AddSingleton<IDomainHandler, EnergyDemandHandler>();
        services.AddSingleton<IDomainHandler, OutdoorScoreHandler>();
        services.AddSingleton<IDomainHandler, BBQScoreHandler>();
        services.AddSingleton<IDomainHandler, ApproachingFrontHandler>();

        return services;
    }
}
