using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Features.Alerts.Dust;
using Njord.Features.Alerts.FloodRain;
using Njord.Features.Alerts.FloodRisk;
using Njord.Features.Alerts.Fog;
using Njord.Features.Alerts.Frost;
using Njord.Features.Alerts.Heat;
using Njord.Features.Alerts.HeavyRain;
using Njord.Features.Alerts.Humidity;
using Njord.Features.Alerts.Ice;
using Njord.Features.Alerts.Pm25;
using Njord.Features.Alerts.Pollen;
using Njord.Features.Alerts.PressureDrop;
using Njord.Features.Alerts.Snow;
using Njord.Features.Alerts.Storm;
using Njord.Features.Alerts.SwimmingConditions;
using Njord.Features.Alerts.Thunderstorm;
using Njord.Features.Alerts.TropicalNight;
using Njord.Features.Alerts.Uv;
using Njord.Features.Alerts.Visibility;
using Njord.Features.Alerts.WindChill;
using Njord.Features.Conditions.ApproachingFront;
using Njord.Features.Conditions.CommuteSafety;
using Njord.Features.Conditions.ExerciseAdvisory;
using Njord.Features.Conditions.FloodTimeline;
using Njord.Features.Conditions.Gardening;
using Njord.Features.Conditions.NightVentilation;
using Njord.Features.Conditions.PollenTrend;
using Njord.Features.Conditions.SatelliteRadiation;
using Njord.Features.Conditions.SolarYield;
using Njord.Features.Conditions.SurfConditions;
using Njord.Features.Conditions.VentilationWindow;
using Njord.Features.Forecasts.SeasonalOutlook;
using Njord.Features.Scores.Bbq;
using Njord.Features.Scores.Laundry;
using Njord.Features.Scores.Outdoor;
using Njord.Features.Trends.ConfidenceTrend;

namespace Njord.Features;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        // Alerts
        services.AddNjordOptions<DustConfig, DustConfigValidator>();
        services.AddNjordOptions<FloodRainConfig>();
        services.AddNjordOptions<FloodRiskConfig>();
        services.AddNjordOptions<FogConfig>();
        services.AddNjordOptions<FrostConfig, FrostConfigValidator>();
        services.AddNjordOptions<HeatConfig, HeatConfigValidator>();
        services.AddNjordOptions<HeavyRainConfig>();
        services.AddNjordOptions<HumidityConfig, HumidityConfigValidator>();
        services.AddNjordOptions<IceConfig>();
        services.AddNjordOptions<Pm25Config>();
        services.AddNjordOptions<PollenConfig>();
        services.AddNjordOptions<PressureDropConfig>();
        services.AddNjordOptions<SnowConfig>();
        services.AddNjordOptions<StormConfig, StormConfigValidator>();
        services.AddNjordOptions<SwimmingConditionsConfig>();
        services.AddNjordOptions<ThunderstormConfig>();
        services.AddNjordOptions<TropicalNightConfig, TropicalNightConfigValidator>();
        services.AddNjordOptions<UvConfig>();
        services.AddNjordOptions<VisibilityConfig, VisibilityConfigValidator>();
        services.AddNjordOptions<WindChillConfig, WindChillConfigValidator>();
        services.AddSingleton<IFeatureRule, DustAlert>();
        services.AddSingleton<IFeatureRule, FloodRainAlert>();
        services.AddSingleton<IFeatureRule, FloodRiskAlert>();
        services.AddSingleton<IFeatureRule, FogAlert>();
        services.AddSingleton<IFeatureRule, FrostAlert>();
        services.AddSingleton<IFeatureRule, HeatAlert>();
        services.AddSingleton<IFeatureRule, HeavyRainAlert>();
        services.AddSingleton<IFeatureRule, HumidityAlert>();
        services.AddSingleton<IFeatureRule, IceAlert>();
        services.AddSingleton<IFeatureRule, Pm25Alert>();
        services.AddSingleton<IFeatureRule, PollenAlert>();
        services.AddSingleton<IFeatureRule, PressureDropAlert>();
        services.AddSingleton<IFeatureRule, SnowAlert>();
        services.AddSingleton<IFeatureRule, StormAlert>();
        services.AddSingleton<IFeatureRule, SwimmingConditionsAlert>();
        services.AddSingleton<IFeatureRule, ThunderstormAlert>();
        services.AddSingleton<IFeatureRule, TropicalNightAlert>();
        services.AddSingleton<IFeatureRule, UvAlert>();
        services.AddSingleton<IFeatureRule, VisibilityAlert>();
        services.AddSingleton<IFeatureRule, WindChillAlert>();

        // Scores
        services.AddNjordOptions<BbqConfig>();
        services.AddNjordOptions<LaundryConfig>();
        services.AddNjordOptions<OutdoorConfig>();
        services.AddSingleton<IFeatureRule, BbqScore>();
        services.AddSingleton<IFeatureRule, LaundryScore>();
        services.AddSingleton<IFeatureRule, OutdoorScore>();

        // Conditions
        services.AddNjordOptions<SurfConditionsConfig>();
        services.AddNjordOptions<ApproachingFrontConfig>();
        services.AddNjordOptions<PollenTrendConfig>();
        services.AddNjordOptions<SatelliteRadiationConfig>();
        services.AddNjordOptions<SolarYieldConfig>();
        services.AddNjordOptions<CommuteSafetyConfig>();
        services.AddNjordOptions<ExerciseAdvisoryConfig>();
        services.AddNjordOptions<FloodTimelineConfig>();
        services.AddNjordOptions<GardeningConfig>();
        services.AddNjordOptions<NightVentilationConfig>();
        services.AddNjordOptions<VentilationWindowConfig>();
        services.AddSingleton<IFeatureRule, SurfConditions>();
        services.AddSingleton<IFeatureRule, ApproachingFront>();
        services.AddSingleton<IFeatureRule, PollenTrend>();
        services.AddSingleton<IFeatureRule, SatelliteRadiation>();
        services.AddSingleton<IFeatureRule, SolarYield>();
        services.AddSingleton<IFeatureRule, CommuteSafety>();
        services.AddSingleton<IFeatureRule, ExerciseAdvisory>();
        services.AddSingleton<IFeatureRule, FloodTimeline>();
        services.AddSingleton<IFeatureRule, Gardening>();
        services.AddSingleton<IFeatureRule, NightVentilation>();
        services.AddSingleton<IFeatureRule, VentilationWindow>();

        // Trends
        services.AddNjordOptions<ConfidenceTrendConfig>();
        services.AddSingleton<IFeatureRule, ConfidenceTrend>();

        // Forecasts
        services.AddNjordOptions<SeasonalOutlookConfig>();
        services.AddSingleton<IFeatureRule, SeasonalOutlook>();

        return services;
    }
}
