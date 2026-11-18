using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class PollWorkerRoundtripTests
{
    private static readonly DateTimeOffset _utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    public static TheoryData<object> Events() =>
    [
        new PollingStarted("lucerne", [(int)0, (int)1],
            new Dictionary<int, TimeSpan> { [0] = TimeSpan.FromMinutes(60), [1] = TimeSpan.FromMinutes(60) },
            _utc, 47.05, 8.31),
        new PollingStopped(_utc),
        new DomainPolled(0, PersistedDomainPollOutcome.Changed, 42, _utc, null),
        new DomainPolled(1, PersistedDomainPollOutcome.Unchanged, null, _utc, null),
        new DomainPolled(2, PersistedDomainPollOutcome.Failed, null, _utc, TimeSpan.FromMinutes(1)),
        new DomainPolled(3, PersistedDomainPollOutcome.NoData, null, _utc, TimeSpan.FromHours(24)),
    ];

    [Fact]
    public void Populated_worker_state_roundtrip()
    {
        var state = new PersistedPollWorkerState(
            "lucerne", true,
            [
                new(0, 7, PersistedPollPhase.Active, _utc.AddHours(-6), _utc.AddHours(6), 0, TimeSpan.FromMinutes(60)),
                new(1, null),
            ],
            47.05, 8.31);

        var recovered = PersistenceRoundtripHelper.Roundtrip(state);

        Assert.Multiple(
            () => Assert.Equal(state with { DomainStates = null }, recovered with { DomainStates = null }),
            () => Assert.Equal(state.DomainStates, recovered.DomainStates));
    }

    [Fact]
    public void Suspended_domain_state_roundtrip()
    {
        var state = new PersistedPollWorkerState(
            "barcelona", true,
            [
                new(4, null, PersistedPollPhase.Suspended, null, _utc.AddHours(24)),
            ],
            41.39, 2.17);

        var recovered = PersistenceRoundtripHelper.Roundtrip(state);

        Assert.Multiple(
            () => Assert.Equal(state with { DomainStates = null }, recovered with { DomainStates = null }),
            () => Assert.Equal(state.DomainStates, recovered.DomainStates));
    }

    [Fact]
    public void Unstarted_worker_state_roundtrip()
    {
        var empty = new PersistedPollWorkerState(null, false, null);

        Assert.Equal(empty, PersistenceRoundtripHelper.Roundtrip(empty));
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void Poll_worker_events_roundtrip(object evt)
    {
        var recovered = PersistenceRoundtripHelper.Roundtrip(evt);

        Assert.IsType(evt.GetType(), recovered);

        if (evt is PollingStarted original && recovered is PollingStarted restored)
        {
            Assert.Multiple(
                () => Assert.Equal(original.Location, restored.Location),
                () => Assert.Equal(original.Domains, restored.Domains),
                () => Assert.Equal(original.DomainIntervals, restored.DomainIntervals),
                () => Assert.Equal(original.Utc, restored.Utc),
                () => Assert.Equal(original.Latitude, restored.Latitude),
                () => Assert.Equal(original.Longitude, restored.Longitude));
        }
        else
        {
            Assert.Equal(evt, recovered);
        }
    }
}
