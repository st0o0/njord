using Njord.Core;
using Njord.Messages.Alerts;

namespace Njord.Messages.Pipeline;

public sealed record FrostAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double ExpectedLow,
    int? EarliestFrostHours) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record HeatAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double ExpectedMax) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record StormAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double ExpectedMaxGust) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record HeavyRainAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    int HourlyExceedModels,
    int DailyExceedModels) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record UvAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    UvLevel UvLevel,
    double UvIndex) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue, HoursUntil,
    DurationHours, Level);

public sealed record FogAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    int FogHours) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue, HoursUntil,
    DurationHours, Level);

public sealed record SnowAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double ExpectedAccumulation,
    double? FreezingLevel) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record PressureDropAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double MaxDrop) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue, HoursUntil,
    DurationHours, Level);

public sealed record ThunderstormAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record IceAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double ExpectedLow,
    int RainHours,
    bool SoilFrozen) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record WindChillAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double FeltTemperature,
    double WindFactor,
    ExposureRisk? ExposureRisk) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record VisibilityAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double MinVisibility,
    int HoursBelow1000m) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record TropicalNightAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double NightMinimum,
    int HoursAbove20) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record HumidityAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double MaxDewpoint,
    ComfortLevel? ComfortLevel) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record PollenAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record Pm25AlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record DustAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record FloodRiskAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record FloodRainAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record RainProbabilityAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double PeakProbability) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record WetBulbDangerAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double PeakWetBulb) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record ClearSkyUvAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double PeakClearSkyUv) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record SwimmingConditionsAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record ShowerAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double PeakIntensity) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record SmogRiskAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double BoundaryLayerHeight,
    double Pm25) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record FireRiskAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double RiskScore) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record PollenBlastAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double PeakPollen) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);

public sealed record CoastalFloodAlertResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level,
    double SeaLevelDeviation,
    double? MaxWaveHeight) : AlertFeatureResult(Location, Severity, Confidence, TriggerValue, Threshold, PeakValue,
    HoursUntil, DurationHours, Level);
