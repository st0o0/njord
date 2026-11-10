using Njord.Core;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Results;

namespace Njord.Grpc;

internal sealed record ClearedAlertResult(Location Location) : AlertFeatureResult(
    Location,
    AlertSeverity.None,
    Confidence: 0,
    TriggerValue: 0,
    Threshold: 0,
    PeakValue: null,
    HoursUntil: null,
    DurationHours: null,
    ConfidenceLevel.Low);

internal sealed record DefaultScoreResult(Location Location) : ScoreFeatureResult(
    Location,
    Score: 0,
    BestStart: DateTimeOffset.MinValue,
    BestHours: 0);

internal sealed record DefaultConditionResult(Location Location) : ConditionFeatureResult(Location);

internal static class DefaultResultFactory
{
    public static FeatureResult? Create(FeatureDescriptor descriptor, string locationName)
    {
        var location = new Location(locationName, 0, 0);
        return descriptor.Output switch
        {
            OutputKind.Alert => new ClearedAlertResult(location),
            OutputKind.Score => new DefaultScoreResult(location),
            OutputKind.Condition => new DefaultConditionResult(location),
            OutputKind.Observation => new ObservationResult(location, descriptor.Name, 0),
            _ => null,
        };
    }
}
