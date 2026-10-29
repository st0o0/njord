using System.Collections.Immutable;
using Njord.Core;

namespace Njord.Messages.Results.Conditions;

public enum CommuteLevel
{
    Safe,
    Caution,
    Dangerous,
}

public enum CommuteHazard
{
    Ice,
    Fog,
    HeavyRain,
    StrongWind,
}

public enum ExerciseLevel
{
    Safe,
    Caution,
    Avoid,
}

public enum ExerciseFactor
{
    Pm25,
    Ozone,
    Temperature,
    Humidity,
    Uv,
}

public enum GardeningAdvice
{
    Plant,
    Hold,
}

public enum FloodStatus
{
    Normal,
    AboveWarning,
}

public enum HeatPumpRecommendation
{
    Ideal,
    Normal,
    Inefficient,
    Avoid,
}

public enum BatteryStrategy
{
    FeedIn,
    ChargeBattery,
    Conserve,
    Normal,
}

public enum IrrigationAdvice
{
    Water,
    Skip,
    Reduce,
    SoilWet,
}

public enum FrostDepthRisk
{
    None,
    Surface,
    Shallow,
    Moderate,
    Deep,
}

public enum InversionStrength
{
    None,
    Mild,
    Strong,
    Extreme,
}

public enum InversionType
{
    Radiation,
    Subsidence,
    Frontal,
}

public enum StabilityClass
{
    Stable,
    Marginal,
    Capped,
    Unstable,
    Explosive,
}

public enum CeilingCategory
{
    VeryLow,
    Low,
    Moderate,
    High,
    VeryHigh,
    Unlimited,
}

public sealed record CommuteSafetyResult(Location Location, CommuteLevel Level, ImmutableArray<CommuteHazard> Hazards) : ConditionFeatureResult(Location);

public sealed record VentilationWindowResult(Location Location, DateTimeOffset BestStart, DateTimeOffset BestEnd) : ConditionFeatureResult(Location);

public sealed record NightVentilationResult(Location Location, DateTimeOffset OpenAt, DateTimeOffset CloseAt, double LowTemperature, double TomorrowPeak) : ConditionFeatureResult(Location);

public sealed record ExerciseAdvisoryResult(Location Location, ExerciseLevel Level, ImmutableArray<ExerciseFactor> Reasons, DateTimeOffset BestWindow) : ConditionFeatureResult(Location);

public sealed record GardeningResult(Location Location, GardeningAdvice Advice, bool PlantingSafe, bool WateringNeeded, bool FrostRisk) : ConditionFeatureResult(Location);

public sealed record FloodTimelineResult(Location Location, FloodStatus Status, DateTimeOffset PeakTime, double PeakDischarge, int DaysAboveWarning) : ConditionFeatureResult(Location);

public sealed record HeatPumpCopConditionResult(Location Location, double Cop, HeatPumpRecommendation Recommendation, double BestCop, int BestHour) : ConditionFeatureResult(Location);

public sealed record SolarBatteryStrategyConditionResult(Location Location, BatteryStrategy Strategy, double TodayKwh, double TomorrowKwhEstimate) : ConditionFeatureResult(Location);

public sealed record IrrigationNeedConditionResult(Location Location, IrrigationAdvice Advice, double SoilMoisturePercent, double RainExpectedMm, double EtMm) : ConditionFeatureResult(Location);

public sealed record FrostDepthConditionResult(Location Location, double DepthCm, FrostDepthRisk Risk, double SnowInsulationCm, double SoilTemperatureC) : ConditionFeatureResult(Location);

public sealed record InversionDetectionConditionResult(Location Location, InversionStrength Strength, InversionType InversionType, double TemperatureDifferenceC, double BoundaryLayerHeightM) : ConditionFeatureResult(Location);

public sealed record AtmosphericStabilityConditionResult(Location Location, StabilityClass StabilityClass, double CapeJPerKg, double CinJPerKg, double LiftedIndex) : ConditionFeatureResult(Location);

public sealed record CloudCeilingConditionResult(Location Location, CeilingCategory CeilingCategory, int EstimatedCeilingM, int CloudCoverLowPercent, int CloudCoverMidPercent, int CloudCoverHighPercent) : ConditionFeatureResult(Location);

public sealed record SolarYieldResult(Location Location, double KwhPerKwp, int PeakHour) : ConditionFeatureResult(Location);

public sealed record SatelliteRadiationResult(Location Location, DateTimeOffset ObservedAt, double Gti, double? Ghi, double? BlendedKwhPerSquareMeter) : ConditionFeatureResult(Location);

public sealed record ApproachingFrontResult(Location Location, double EstimatedInHours, double PressureDrop, double TemperatureDrop) : ConditionFeatureResult(Location);

public enum SnowDepthLevel { None, Light, Moderate, Heavy, Extreme }

public sealed record SnowDepthWatchResult(Location Location, SnowDepthLevel Level, double DepthCm) : ConditionFeatureResult(Location);

public enum SoilMoistureLevel { Dry, Optimal, Wet }

public sealed record SoilMoistureResult(Location Location, SoilMoistureLevel Level, double Moisture) : ConditionFeatureResult(Location);

public enum EvapotranspirationLevel { Low, Moderate, High }

public sealed record EvapotranspirationResult(Location Location, EvapotranspirationLevel Level, double DailyMm) : ConditionFeatureResult(Location);

public sealed record RagweedPollenResult(Location Location, double PeakValue) : ConditionFeatureResult(Location);

public sealed record GranularAqiResult(Location Location, double? Pm25Aqi, double? Pm10Aqi, double? No2Aqi, double? OzoneAqi, double? So2Aqi) : ConditionFeatureResult(Location);

public enum CurrentStrength { Weak, Moderate, Strong, VeryStrong }

public sealed record OceanCurrentsResult(Location Location, CurrentStrength Strength, double PeakVelocityKmh, double? Direction) : ConditionFeatureResult(Location);

public sealed record ElevatedWindResult(Location Location, double Speed80m, double Direction80m, double? Speed120m, double? Direction120m) : ConditionFeatureResult(Location);
