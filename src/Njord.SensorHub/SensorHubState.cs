using System.Collections.Immutable;
using Njord.Weather;
using Njord.Messages.Sensors;
using Njord.Sensors;

namespace Njord.SensorHub;

public sealed record SensorHubState(
    ImmutableDictionary<(string Location, SensorKind Kind, string Source), SensorReading> Readings)
{
    public static readonly SensorHubState Empty =
        new(ImmutableDictionary<(string Location, SensorKind Kind, string Source), SensorReading>.Empty);
}

public sealed record ValidationResult(bool Accepted, string? RejectionReason);

public static class SensorHubStateExtensions
{
    public static SensorHubState Apply(this SensorHubState state, SensorReading reading) =>
        state with { Readings = state.Readings.SetItem((reading.Location, reading.Kind, reading.Source), reading) };

    public static ValidationResult Validate(this SensorHubState _, SensorReading reading)
    {
        if (!SensorKindMetadata.TryGet(reading.Kind, out var metadata) || metadata is null)
            return new ValidationResult(false, $"Unknown SensorKind: {reading.Kind}");

        if (!metadata.IsPlausible(reading.Value))
            return new ValidationResult(false,
                $"Value {reading.Value} outside plausible range [{metadata.Min}, {metadata.Max}] for {reading.Kind}");

        return new ValidationResult(true, null);
    }

    public static SensorSnapshotResponse GetSnapshot(
        this SensorHubState state, string location, DateTimeOffset now, TimeSpan staleness)
    {
        var readings = new Dictionary<SensorKind, AggregatedReading>();

        var byKind = state.Readings
            .Where(kvp => kvp.Key.Location == location && !IsExpired(kvp.Value, now, staleness))
            .GroupBy(kvp => kvp.Key.Kind);

        foreach (var group in byKind)
        {
            var metadata = SensorKindMetadata.Get(group.Key);
            var values = group.Select(kvp => kvp.Value).ToList();
            var aggregated = Aggregate(values, metadata.Aggregation);
            if (aggregated is not null)
            {
                readings[group.Key] = aggregated;
            }
        }

        return readings.Count > 0
            ? new SensorSnapshotResult(new SensorSnapshot(location, readings))
            : new SensorSnapshotNotAvailable(new LocationId(location));
    }

    public static SensorHubState Expire(this SensorHubState state, DateTimeOffset now, TimeSpan staleness)
    {
        var expired = state.Readings
            .Where(kvp => IsExpired(kvp.Value, now, staleness))
            .Select(kvp => kvp.Key)
            .ToList();

        if (expired.Count == 0)
            return state;

        return state with { Readings = state.Readings.RemoveRange(expired) };
    }

    private static bool IsExpired(SensorReading reading, DateTimeOffset now, TimeSpan staleness) =>
        now - reading.MeasuredAt > staleness;

    private static AggregatedReading? Aggregate(List<SensorReading> values, AggregationStrategy strategy)
    {
        if (values.Count == 0)
            return null;

        var newest = values.Max(v => v.MeasuredAt);
        var count = values.Count;

        var aggregatedValue = strategy switch
        {
            AggregationStrategy.Average => values.Average(v => v.Value),
            AggregationStrategy.Sum => values.Sum(v => v.Value),
            AggregationStrategy.Latest => values.OrderByDescending(v => v.MeasuredAt).First().Value,
            _ => values.Average(v => v.Value),
        };

        return new AggregatedReading(aggregatedValue, count, newest);
    }
}
