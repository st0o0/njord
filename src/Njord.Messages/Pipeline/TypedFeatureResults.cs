using Njord.Forecast;
using Njord.Messages.Alerts;

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
    ConfidenceLevel Level) : PipelineEvent;

public abstract record ScoreFeatureResult(
    Location Location,
    int Score,
    DateTimeOffset BestStart,
    int BestHours) : PipelineEvent;

public abstract record ConditionFeatureResult(
    Location Location,
    string State) : PipelineEvent;

public abstract record TrendFeatureResult(
    Location Location,
    string Direction,
    double Magnitude) : PipelineEvent;

public abstract record ForecastFeatureResult(
    Location Location) : PipelineEvent;
