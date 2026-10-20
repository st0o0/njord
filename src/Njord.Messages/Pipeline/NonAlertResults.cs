using System.Collections.Immutable;
using Njord.Forecast;

namespace Njord.Messages.Pipeline;

public sealed record BbqScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
public sealed record LaundryScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
public sealed record OutdoorScoreResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);
public sealed record SurfConditionsResult(Location Location, int Score, DateTimeOffset BestStart, int BestHours) : ScoreFeatureResult(Location, Score, BestStart, BestHours);

public sealed record CommuteSafetyResult(Location Location, string State, IReadOnlyList<string> Risks) : ConditionFeatureResult(Location, State);
public sealed record VentilationWindowResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record NightVentilationResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record ExerciseAdvisoryResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record GardeningResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record FloodTimelineResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);

public sealed record SeasonalOutlookResult(Location Location, ImmutableArray<ForecastEntry> Entries) : ForecastFeatureResult(Location);
public sealed record SolarYieldResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record SatelliteRadiationResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record ConfidenceTrendResult(Location Location, string Direction, double Magnitude, IReadOnlyDictionary<string, string> Data) : TrendFeatureResult(Location, Direction, Magnitude);
public sealed record ApproachingFrontResult(Location Location, string State, IReadOnlyDictionary<string, string> Data) : ConditionFeatureResult(Location, State);
public sealed record PollenTrendResult(Location Location, string Direction, double Magnitude, IReadOnlyDictionary<string, string> Data) : TrendFeatureResult(Location, Direction, Magnitude);
