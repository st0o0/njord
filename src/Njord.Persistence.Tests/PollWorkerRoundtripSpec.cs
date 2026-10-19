using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class PollWorkerRoundtripSpec
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    public static TheoryData<object> Events() =>
    [
        new PollingStarted("lucerne", TimeSpan.FromMinutes(20), Utc),
        new PollingStopped(Utc),
        new ForecastChanged(42, Utc),
        new ForecastUnchanged(Utc),
        new PollFailed(Utc, TimeSpan.FromMinutes(5)),
        new PollDeferred(Utc, TimeSpan.FromSeconds(5)),
    ];

    [Fact]
    public void Populated_worker_state_roundtrip()
    {
        var state = new PersistedPollWorkerState(
            "lucerne", true, TimeSpan.FromMinutes(20), 42, Utc, Utc.AddHours(-3), Utc.AddHours(3),
            2, PersistedPollPhase.Steady, TimeSpan.FromHours(3), 1,
            new List<PersistedDomainState> { new(0, 7), new(1, null) });

        var recovered = PersistenceRoundtripHelper.Roundtrip(state);

        Assert.Multiple(
            () => Assert.Equal(state with { DomainStates = null }, recovered with { DomainStates = null }),
            () => Assert.Equal(state.DomainStates, recovered.DomainStates));
    }

    [Fact]
    public void Unstarted_worker_state_roundtrip()
    {
        var empty = new PersistedPollWorkerState(
            null, false, TimeSpan.Zero, null, null, null, DateTimeOffset.MinValue, 0,
            PersistedPollPhase.Discovery, null, 0, null);

        Assert.Equal(empty, PersistenceRoundtripHelper.Roundtrip(empty));
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void Poll_worker_events_roundtrip(object evt)
    {
        var recovered = PersistenceRoundtripHelper.Roundtrip(evt);

        Assert.Multiple(
            () => Assert.IsType(evt.GetType(), recovered),
            () => Assert.Equal(evt, recovered));
    }
}
