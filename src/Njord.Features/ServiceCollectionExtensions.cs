using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration;
using Njord.Features.Alerts.ClearSkyUv;
using Njord.Features.Alerts.CoastalFlood;
using Njord.Features.Alerts.CompoundHeatStress;
using Njord.Features.Alerts.Dust;
using Njord.Features.Alerts.FireRisk;
using Njord.Features.Alerts.FloodRain;
using Njord.Features.Alerts.FloodRisk;
using Njord.Features.Alerts.Fog;
using Njord.Features.Alerts.Frost;
using Njord.Features.Alerts.Heat;
using Njord.Features.Alerts.HeavyRain;
using Njord.Features.Alerts.Humidity;
using Njord.Features.Alerts.Ice;
using Njord.Features.Alerts.MigraineRisk;
using Njord.Features.Alerts.Pm25;
using Njord.Features.Alerts.Pollen;
using Njord.Features.Alerts.PollenBlast;
using Njord.Features.Alerts.PressureDrop;
using Njord.Features.Alerts.RainProbability;
using Njord.Features.Alerts.Shower;
using Njord.Features.Alerts.SmogRisk;
using Njord.Features.Alerts.Snow;
using Njord.Features.Alerts.Storm;
using Njord.Features.Alerts.SwimmingConditions;
using Njord.Features.Alerts.Thunderstorm;
using Njord.Features.Alerts.TropicalNight;
using Njord.Features.Alerts.Uv;
using Njord.Features.Alerts.Visibility;
using Njord.Features.Alerts.WetBulbDanger;
using Njord.Features.Alerts.WindChill;
using Njord.Features.Conditions.ApproachingFront;
using Njord.Features.Conditions.AtmosphericStability;
using Njord.Features.Conditions.CloudCeiling;
using Njord.Features.Conditions.CommuteSafety;
using Njord.Features.Conditions.ElevatedWind;
using Njord.Features.Conditions.Evapotranspiration;
using Njord.Features.Conditions.ExerciseAdvisory;
using Njord.Features.Conditions.FloodTimeline;
using Njord.Features.Conditions.FrostDepth;
using Njord.Features.Conditions.Gardening;
using Njord.Features.Conditions.GranularAqi;
using Njord.Features.Conditions.HeatPumpCop;
using Njord.Features.Conditions.InversionDetection;
using Njord.Features.Conditions.IrrigationNeed;
using Njord.Features.Conditions.NightVentilation;
using Njord.Features.Conditions.OceanCurrents;
using Njord.Features.Conditions.PollenTrend;
using Njord.Features.Conditions.RagweedPollen;
using Njord.Features.Conditions.SatelliteRadiation;
using Njord.Features.Conditions.SnowDepthWatch;
using Njord.Features.Conditions.SoilMoisture;
using Njord.Features.Conditions.SolarYield;
using Njord.Features.Conditions.SurfConditions;
using Njord.Features.Conditions.VentilationWindow;
using Njord.Features.Forecasts.GrowingDegree;
using Njord.Features.Forecasts.ModelComparison;
using Njord.Features.Forecasts.SeasonalOutlook;
using Njord.Features.Scores.AllergySeverity;
using Njord.Features.Scores.AsthmaRisk;
using Njord.Features.Scores.Bbq;
using Njord.Features.Scores.Laundry;
using Njord.Features.Scores.Outdoor;
using Njord.Features.Scores.SleepQuality;
using Njord.Features.Scores.SprayWindow;
using Njord.Features.Trends.ConfidenceTrend;
using Njord.Messages.Results;

namespace Njord.Features;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFeature<TRule, TConfig>(this IServiceCollection services)
        where TRule : class, IFeatureRule
        where TConfig : class, IConfigSection
    {
        services.AddNjordOptions<TConfig>();
        services.AddSingleton<IFeatureRule, TRule>();
        return services;
    }

    public static IServiceCollection AddFeature<TRule, TConfig, TValidator>(this IServiceCollection services)
        where TRule : class, IFeatureRule
        where TConfig : class, IConfigSection
        where TValidator : class, IValidateOptions<TConfig>
    {
        services.AddNjordOptions<TConfig, TValidator>();
        services.AddSingleton<IFeatureRule, TRule>();
        return services;
    }

    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        // Alerts
        services.AddFeature<ClearSkyUvAlert, ClearSkyUvConfig, ClearSkyUvConfigValidator>();
        services.AddFeature<CoastalFloodAlert, CoastalFloodConfig>();
        services.AddFeature<CompoundHeatStressAlert, CompoundHeatStressConfig, CompoundHeatStressConfigValidator>();
        services.AddFeature<DustAlert, DustConfig, DustConfigValidator>();
        services.AddFeature<FireRiskAlert, FireRiskConfig, FireRiskConfigValidator>();
        services.AddFeature<FloodRainAlert, FloodRainConfig, FloodRainConfigValidator>();
        services.AddFeature<FloodRiskAlert, FloodRiskConfig, FloodRiskConfigValidator>();
        services.AddFeature<FogAlert, FogConfig>();
        services.AddFeature<FrostAlert, FrostConfig, FrostConfigValidator>();
        services.AddFeature<HeatAlert, HeatConfig, HeatConfigValidator>();
        services.AddFeature<HeavyRainAlert, HeavyRainConfig>();
        services.AddFeature<HumidityAlert, HumidityConfig, HumidityConfigValidator>();
        services.AddFeature<IceAlert, IceConfig>();
        services.AddFeature<MigraineRiskAlert, MigraineRiskConfig>();
        services.AddFeature<Pm25Alert, Pm25Config, Pm25ConfigValidator>();
        services.AddFeature<PollenAlert, PollenConfig, PollenConfigValidator>();
        services.AddFeature<PollenBlastAlert, PollenBlastConfig>();
        services.AddFeature<PressureDropAlert, PressureDropConfig, PressureDropConfigValidator>();
        services.AddFeature<RainProbabilityAlert, RainProbabilityConfig, RainProbabilityConfigValidator>();
        services.AddFeature<ShowerAlert, ShowerConfig, ShowerConfigValidator>();
        services.AddFeature<SmogRiskAlert, SmogRiskConfig>();
        services.AddFeature<SnowAlert, SnowConfig, SnowConfigValidator>();
        services.AddFeature<StormAlert, StormConfig, StormConfigValidator>();
        services.AddFeature<SwimmingConditionsAlert, SwimmingConditionsConfig>();
        services.AddFeature<ThunderstormAlert, ThunderstormConfig, ThunderstormConfigValidator>();
        services.AddFeature<TropicalNightAlert, TropicalNightConfig, TropicalNightConfigValidator>();
        services.AddFeature<UvAlert, UvConfig, UvConfigValidator>();
        services.AddFeature<VisibilityAlert, VisibilityConfig, VisibilityConfigValidator>();
        services.AddFeature<WetBulbDangerAlert, WetBulbDangerConfig, WetBulbDangerConfigValidator>();
        services.AddFeature<WindChillAlert, WindChillConfig, WindChillConfigValidator>();

        // Scores
        services.AddFeature<AllergySeverityScore, AllergySeverityConfig>();
        services.AddFeature<AsthmaRiskScore, AsthmaRiskConfig>();
        services.AddFeature<BbqScore, BbqConfig>();
        services.AddFeature<LaundryScore, LaundryConfig>();
        services.AddFeature<OutdoorScore, OutdoorConfig>();
        services.AddFeature<SleepQualityScore, SleepQualityConfig>();

        // Conditions
        services.AddFeature<ApproachingFront, ApproachingFrontConfig>();
        services.AddFeature<AtmosphericStabilityCondition, AtmosphericStabilityConfig>();
        services.AddFeature<CloudCeilingCondition, CloudCeilingConfig>();
        services.AddFeature<CommuteSafety, CommuteSafetyConfig>();
        services.AddFeature<ElevatedWind, ElevatedWindConfig>();
        services.AddFeature<Evapotranspiration, EvapotranspirationConfig>();
        services.AddFeature<ExerciseAdvisory, ExerciseAdvisoryConfig>();
        services.AddFeature<FloodTimeline, FloodTimelineConfig>();
        services.AddFeature<FrostDepthCondition, FrostDepthConfig>();
        services.AddFeature<Gardening, GardeningConfig>();
        services.AddFeature<GranularAqi, GranularAqiConfig>();
        services.AddFeature<HeatPumpCopCondition, HeatPumpCopConfig>();
        services.AddFeature<InversionDetectionCondition, InversionDetectionConfig>();
        services.AddFeature<IrrigationNeedCondition, IrrigationNeedConfig>();
        services.AddFeature<NightVentilation, NightVentilationConfig>();
        services.AddFeature<OceanCurrents, OceanCurrentsConfig>();
        services.AddFeature<PollenTrend, PollenTrendConfig>();
        services.AddFeature<RagweedPollen, RagweedPollenConfig>();
        services.AddFeature<SatelliteRadiation, SatelliteRadiationConfig>();
        services.AddFeature<SnowDepthWatch, SnowDepthWatchConfig>();
        services.AddFeature<SoilMoisture, SoilMoistureConfig>();
        services.AddFeature<SolarYield, SolarYieldConfig, SolarYieldConfigValidator>();
        services.AddFeature<SprayWindowScore, SprayWindowConfig>();
        services.AddFeature<SurfConditions, SurfConditionsConfig>();
        services.AddFeature<VentilationWindow, VentilationWindowConfig>();

        // Trends
        services.AddFeature<ConfidenceTrend, ConfidenceTrendConfig>();

        // Forecasts
        services.AddFeature<GrowingDegree, GrowingDegreeConfig>();
        services.AddFeature<ModelComparison, ModelComparisonConfig>();
        services.AddFeature<SeasonalOutlook, SeasonalOutlookConfig>();

        return services;
    }
}
