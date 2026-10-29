using Njord.Core;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Results;

namespace Njord.Messages.Pipeline;

public abstract record AlertFeatureResult(
    Location Location,
    AlertSeverity Severity,
    double Confidence,
    double TriggerValue,
    double Threshold,
    double? PeakValue,
    int? HoursUntil,
    int? DurationHours,
    ConfidenceLevel Level) : FeatureResult(Location);

public abstract record ScoreFeatureResult(
    Location Location,
    int Score,
    DateTimeOffset BestStart,
    int BestHours) : FeatureResult(Location)
{
    public IReadOnlyList<ActivityWindow> Windows { get; init; } = [];
}

public abstract record ConditionFeatureResult(Location Location) : FeatureResult(Location);

public abstract record TrendFeatureResult(Location Location) : FeatureResult(Location);

public abstract record ForecastFeatureResult(Location Location) : FeatureResult(Location);
