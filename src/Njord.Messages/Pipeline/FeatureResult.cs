using System.Collections.Immutable;
using Njord.Forecast;
using Njord.Messages.Alerts;

namespace Njord.Messages.Pipeline;

public abstract record FeatureResult(Location Location, string WireKey, OutputKind Output) : PipelineEvent;

public sealed record AlertResult(Location Location, string WireKey, Alert Alert, DateTimeOffset Timestamp)
    : FeatureResult(Location, WireKey, OutputKind.Alert);

public sealed record ScoreResult(Location Location, string WireKey, int Score, DateTimeOffset BestStart, int BestHours)
    : FeatureResult(Location, WireKey, OutputKind.Score);

public sealed record ConditionResult(Location Location, string WireKey, string State, IReadOnlyDictionary<string, string> Data)
    : FeatureResult(Location, WireKey, OutputKind.Condition);

public sealed record TrendResult(Location Location, string WireKey, string Direction, double Magnitude, IReadOnlyDictionary<string, string> Data)
    : FeatureResult(Location, WireKey, OutputKind.Trend);

public sealed record ForecastResult(Location Location, string WireKey, ImmutableArray<ForecastEntry> Entries)
    : FeatureResult(Location, WireKey, OutputKind.Forecast);

public sealed record ForecastEntry(string Label, IReadOnlyDictionary<string, string> Values);
