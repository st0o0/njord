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
        services.AddNjordFeatureOptions<DustConfig>();
        services.AddNjordFeatureOptions<FloodRainConfig>();
        services.AddNjordFeatureOptions<FloodRiskConfig>();
        services.AddNjordFeatureOptions<FogConfig>();
        services.AddNjordFeatureOptions<FrostConfig>();
        services.AddNjordFeatureOptions<HeatConfig>();
        services.AddNjordFeatureOptions<HeavyRainConfig>();
        services.AddNjordFeatureOptions<HumidityConfig>();
        services.AddNjordFeatureOptions<IceConfig>();
        services.AddNjordFeatureOptions<Pm25Config>();
        services.AddNjordFeatureOptions<PollenConfig>();
        services.AddNjordFeatureOptions<PressureDropConfig>();
        services.AddNjordFeatureOptions<SnowConfig>();
        services.AddNjordFeatureOptions<StormConfig>();
        services.AddNjordFeatureOptions<SwimmingConditionsConfig>();
        services.AddNjordFeatureOptions<ThunderstormConfig>();
        services.AddNjordFeatureOptions<TropicalNightConfig>();
        services.AddNjordFeatureOptions<UvConfig>();
        services.AddNjordFeatureOptions<VisibilityConfig>();
        services.AddNjordFeatureOptions<WindChillConfig>();
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
        services.AddNjordFeatureOptions<BbqConfig>();
        services.AddNjordFeatureOptions<LaundryConfig>();
        services.AddNjordFeatureOptions<OutdoorConfig>();
        services.AddSingleton<IFeatureRule, BbqScore>();
        services.AddSingleton<IFeatureRule, LaundryScore>();
        services.AddSingleton<IFeatureRule, OutdoorScore>();

        // Conditions
        services.AddNjordFeatureOptions<SurfConditionsConfig>();
        services.AddNjordFeatureOptions<ApproachingFrontConfig>();
        services.AddNjordFeatureOptions<PollenTrendConfig>();
        services.AddNjordFeatureOptions<SatelliteRadiationConfig>();
        services.AddNjordFeatureOptions<SolarYieldConfig>();
        services.AddNjordFeatureOptions<CommuteSafetyConfig>();
        services.AddNjordFeatureOptions<ExerciseAdvisoryConfig>();
        services.AddNjordFeatureOptions<FloodTimelineConfig>();
        services.AddNjordFeatureOptions<GardeningConfig>();
        services.AddNjordFeatureOptions<NightVentilationConfig>();
        services.AddNjordFeatureOptions<VentilationWindowConfig>();
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
        services.AddNjordFeatureOptions<ConfidenceTrendConfig>();
        services.AddSingleton<IFeatureRule, ConfidenceTrend>();

        // Forecasts
        services.AddNjordFeatureOptions<SeasonalOutlookConfig>();
        services.AddSingleton<IFeatureRule, SeasonalOutlook>();

        return services;
    }
}
