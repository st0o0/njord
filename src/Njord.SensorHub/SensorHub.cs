using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core.Actors;
using Njord.Core.Configuration.Sensors;
using Njord.Messages.Sensors;
using Servus.Akka;

namespace Njord.SensorHub;

// Pushes the readings of every location with fresh readings to the pipeline, which zips the latest map onto
// each forecast; the pipeline never asks.
public sealed class SensorHub : ReceiveActor, IWithTimers
{
    private static readonly TimeSpan DefaultExpiryInterval = TimeSpan.FromSeconds(60);

    private sealed record ExpireTick;
    private sealed record PipelineResolved;
    private sealed record PipelineResolveFailed(Exception Cause);

    public ITimerScheduler Timers { get; set; } = null!;

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleness;
    private readonly TimeSpan _expiryInterval;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private SensorHubState _state = SensorHubState.Empty;

    public SensorHub(IOptions<SensorOptions> sensorOptions, TimeProvider timeProvider, TimeSpan? expiryInterval = null)
    {
        _expiryInterval = expiryInterval ?? DefaultExpiryInterval;
        _timeProvider = timeProvider;
        _staleness = TimeSpan.FromSeconds(sensorOptions.Value.StalenessSeconds);

        Receive<UpdateReading>(Handle);
        Receive<QuerySensorReadings>(Handle);
        Receive<RequestSensorReadings>(_ => Sender.Tell(CurrentReadings()));
        Receive<ExpireTick>(_ => HandleExpire());
        Receive<PipelineResolved>(_ => PushReadings());
        Receive<PipelineResolveFailed>(msg => _log.Warning(msg.Cause, "Could not resolve the pipeline for sensor pushes"));
    }

    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("expire", new ExpireTick(), _expiryInterval, _expiryInterval);
        // Waits for the pipeline's registration, so the pipeline gets the current map as soon as it exists.
        Context.GetActorAsync<IForecastPipeline>().PipeTo(Self,
            success: _ => new PipelineResolved(),
            failure: ex => new PipelineResolveFailed(ex));
    }

    private void Handle(UpdateReading msg)
    {
        var validation = _state.Validate(msg.Reading);
        if (!validation.Accepted)
        {
            _log.Warning("Rejected reading: {Reason}", validation.RejectionReason);
            Sender.Tell(new PushResult(false, validation.RejectionReason));
            return;
        }

        _state = _state.Apply(msg.Reading);
        Sender.Tell(new PushResult(true, null));
        PushReadings();
    }

    private void Handle(QuerySensorReadings msg)
    {
        var now = _timeProvider.GetUtcNow();
        Sender.Tell(_state.GetReadings(msg.Location.Name, now, _staleness));
    }

    private void HandleExpire()
    {
        var now = _timeProvider.GetUtcNow();
        var before = _state.Readings.Count;
        _state = _state.Expire(now, _staleness);
        var expired = before - _state.Readings.Count;

        if (expired > 0)
        {
            _log.Debug("Expired {Count} stale sensor readings", expired);
        }

        PushReadings();
    }

    private void PushReadings()
    {
        if (!Context.TryGetActor<IForecastPipeline>(out var pipeline))
        {
            return;
        }

        pipeline.Tell(CurrentReadings());
    }

    private LocationSensorReadings CurrentReadings() => _state.GetLocationReadings(_timeProvider.GetUtcNow(), _staleness);
}
