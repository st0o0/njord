using Akka.Actor;
using Akka.Event;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Sensors;
using Njord.Messages.Sensors;

namespace Njord.SensorHub;

public sealed class SensorHub : ReceiveActor, IWithTimers
{
    private sealed record ExpireTick;

    public ITimerScheduler Timers { get; set; } = null!;

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _staleness;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private SensorHubState _state = SensorHubState.Empty;

    public SensorHub(IOptions<SensorOptions> sensorOptions, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _staleness = TimeSpan.FromSeconds(sensorOptions.Value.StalenessSeconds);

        Receive<UpdateReading>(Handle);
        Receive<QuerySensorSnapshot>(Handle);
        Receive<ExpireTick>(_ => HandleExpire());
    }

    protected override void PreStart()
    {
        Timers.StartPeriodicTimer("expire", new ExpireTick(), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
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
    }

    private void Handle(QuerySensorSnapshot msg)
    {
        var now = _timeProvider.GetUtcNow();
        Sender.Tell(_state.GetSnapshot(msg.Location.Name, now, _staleness));
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
    }
}
