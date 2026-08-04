using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Configuration;
using Njord.Domain.Sensors;

namespace Njord.Sensors;

public sealed class SensorHubActor : ReceiveActor, IWithTimers
{
    public ITimerScheduler Timers { get; set; } = null!;

    private readonly Dictionary<(string Location, SensorKind Kind, string Source), SensorReading> _readings = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleness;
    private ILoggingAdapter _log = null!;

    private sealed record ExpireTick;

    public SensorHubActor(IOptions<NjordOptions> options, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _staleness = TimeSpan.FromSeconds(options.Value.Sensors.StalenessSeconds);

        Receive<UpdateReading>(Handle);
        Receive<GetSnapshot>(Handle);
        Receive<ExpireTick>(_ => Expire());
    }

    protected override void PreStart()
    {
        _log = Context.GetLogger();
        Timers.StartPeriodicTimer("expire", new ExpireTick(), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    private void Handle(UpdateReading msg)
    {
        var reading = msg.Reading;

        if (!SensorKindMetadata.TryGet(reading.Kind, out var metadata) || metadata is null)
        {
            _log.Warning("Rejected reading: unknown SensorKind {Kind}", reading.Kind);
            Sender.Tell(new PushResult(false, $"Unknown SensorKind: {reading.Kind}"));
            return;
        }

        if (!metadata.IsPlausible(reading.Value))
        {
            _log.Warning("Rejected reading: {Kind} value {Value} outside range [{Min}, {Max}]",
                reading.Kind, reading.Value, metadata.Min, metadata.Max);
            Sender.Tell(new PushResult(false,
                $"Value {reading.Value} outside plausible range [{metadata.Min}, {metadata.Max}] for {reading.Kind}"));
            return;
        }

        var key = (reading.Location, reading.Kind, reading.Source);
        _readings[key] = reading;
        Sender.Tell(new PushResult(true, null));
    }

    private void Handle(GetSnapshot msg)
    {
        var now = _timeProvider.GetUtcNow();
        var readings = new Dictionary<SensorKind, AggregatedReading>();

        var byKind = _readings
            .Where(kvp => kvp.Key.Location == msg.Location && !IsExpired(kvp.Value, now))
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

        var snapshot = readings.Count > 0
            ? new SensorSnapshot(msg.Location, readings)
            : null;

        Sender.Tell(new SensorSnapshotResponse(snapshot));
    }

    private static AggregatedReading? Aggregate(List<SensorReading> values, AggregationStrategy strategy)
    {
        if (values.Count == 0) return null;

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

    private bool IsExpired(SensorReading reading, DateTimeOffset now)
        => now - reading.MeasuredAt > _staleness;

    private void Expire()
    {
        var now = _timeProvider.GetUtcNow();
        var expired = _readings.Where(kvp => IsExpired(kvp.Value, now)).Select(kvp => kvp.Key).ToList();
        foreach (var key in expired)
        {
            _readings.Remove(key);
        }

        if (expired.Count > 0)
        {
            _log.Debug("Expired {Count} stale sensor readings", expired.Count);
        }
    }
}

public sealed record PushResult(bool Accepted, string? RejectionReason);
