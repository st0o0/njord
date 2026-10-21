using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class PollWorkerRoundtripSpec
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    public static TheoryData<object> Events() =>
    [
        new PollingStarted("lucerne", [(int)0, (int)1], TimeSpan.FromMinutes(20), Utc, 47.05, 8.31),
        new PollingStopped(Utc),
        new DomainPolled(0, PersistedDomainPollOutcome.Changed, 42, Utc, null),
        new DomainPolled(1, PersistedDomainPollOutcome.Unchanged, null, Utc, null),
        new DomainPolled(2, PersistedDomainPollOutcome.Failed, null, Utc, TimeSpan.FromMinutes(1)),
    ];

    [Fact]
    public void Populated_worker_state_roundtrip()
    {
        var state = new PersistedPollWorkerState(
            "lucerne", true, TimeSpan.FromMinutes(20),
            new List<PersistedDomainState>
            {
                new(0, 7, PersistedPollPhase.Steady, TimeSpan.FromHours(6), 2, Utc.AddHours(-6), TimeSpan.FromHours(6), Utc.AddHours(6)),
                new(1, null),
            },
            47.05, 8.31);

        var recovered = PersistenceRoundtripHelper.Roundtrip(state);

        Assert.Multiple(
            () => Assert.Equal(state with { DomainStates = null }, recovered with { DomainStates = null }),
            () => Assert.Equal(state.DomainStates, recovered.DomainStates));
    }

    [Fact]
    public void Unstarted_worker_state_roundtrip()
    {
        var empty = new PersistedPollWorkerState(
            null, false, TimeSpan.Zero, null);

        Assert.Equal(empty, PersistenceRoundtripHelper.Roundtrip(empty));
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void Poll_worker_events_roundtrip(object evt)
    {
        var recovered = PersistenceRoundtripHelper.Roundtrip(evt);

        Assert.IsType(evt.GetType(), recovered);

        // PollingStarted contains int[] which uses reference equality; compare structurally.
        if (evt is PollingStarted original && recovered is PollingStarted restored)
        {
            Assert.Multiple(
                () => Assert.Equal(original.Location, restored.Location),
                () => Assert.Equal(original.Domains, restored.Domains),
                () => Assert.Equal(original.DiscoveryInterval, restored.DiscoveryInterval),
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
