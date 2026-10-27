using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Messages.Alerts;

namespace Njord.Messages.Pipeline;

public sealed record BbqScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
public sealed record LaundryScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
public sealed record OutdoorScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
public sealed record SurfConditionsResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record CommuteSafetyResult(Location Location, CommuteLevel Level, ImmutableArray<CommuteHazard> Hazards) : ConditionFeatureResult(Location);

public sealed record VentilationWindowResult(Location Location, DateTimeOffset BestStart, DateTimeOffset BestEnd) : ConditionFeatureResult(Location);

public sealed record NightVentilationResult(Location Location, DateTimeOffset OpenAt, DateTimeOffset CloseAt, double LowTemperature, double TomorrowPeak) : ConditionFeatureResult(Location);

public sealed record ExerciseAdvisoryResult(Location Location, ExerciseLevel Level, ImmutableArray<ExerciseFactor> Reasons, DateTimeOffset BestWindow) : ConditionFeatureResult(Location);

public sealed record GardeningResult(Location Location, GardeningAdvice Advice, bool PlantingSafe, bool WateringNeeded, bool FrostRisk) : ConditionFeatureResult(Location);

public sealed record FloodTimelineResult(Location Location, FloodStatus Status, DateTimeOffset PeakTime, double PeakDischarge, int DaysAboveWarning) : ConditionFeatureResult(Location);

public sealed record SolarYieldResult(Location Location, double KwhPerKwp, int PeakHour) : ConditionFeatureResult(Location);

public sealed record SatelliteRadiationResult(Location Location, DateTimeOffset ObservedAt, double Gti, double? Ghi, double? BlendedKwhPerSquareMeter) : ConditionFeatureResult(Location);

public sealed record ApproachingFrontResult(Location Location, double EstimatedInHours, double PressureDrop, double TemperatureDrop) : ConditionFeatureResult(Location);

public sealed record ConfidenceTrendResult(Location Location, ConfidenceTrendDirection Direction, double Magnitude, double CurrentAvgStdDev, double PreviousAvgStdDev) : TrendFeatureResult(Location);

public sealed record PollenTrendResult(
    Location Location,
    PollenType? PeakType,
    DateTimeOffset? PeakTime,
    PollenTrendDirection? Birch,
    PollenTrendDirection? Grass,
    PollenTrendDirection? Alder,
    PollenTrendDirection? Mugwort) : TrendFeatureResult(Location);

public sealed record SeasonalMonth(DateOnly Month, double TemperatureAnomaly, double PrecipitationAnomalyPercent, ConfidenceLevel Confidence);

public sealed record SeasonalOutlookResult(Location Location, ImmutableArray<SeasonalMonth> Months) : ForecastFeatureResult(Location);

public sealed record ModelDayValues(string ModelId, double? MaxTemperature, double? MinTemperature, double? Precipitation);

public sealed record ModelComparisonDay(DateOnly Date, ImmutableArray<ModelDayValues> Models);

public sealed record ModelComparisonResult(Location Location, ImmutableArray<ModelComparisonDay> Days) : ForecastFeatureResult(Location);

public sealed record GrowingDegreeDay(DateOnly Date, double Gdd);

public sealed record GrowingDegreeResult(Location Location, ImmutableArray<GrowingDegreeDay> Days, double CumulativeGdd) : ForecastFeatureResult(Location);

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
