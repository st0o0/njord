using Newtonsoft.Json;
using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class PollWorkerRecordsSpec
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly PersistedPollWorkerState Snapshot = new(
        "lucerne", true, TimeSpan.FromMinutes(20), 42, Utc, Utc.AddHours(-3), Utc.AddHours(3).AddMinutes(1),
        2, PersistedPollPhase.Steady, TimeSpan.FromHours(3), 1, null);

    public static TheoryData<object> Events() =>
    [
        new PollingStarted("lucerne",TimeSpan.FromMinutes(20), Utc),
        new PollingStopped(Utc),
        new ForecastChanged(42, Utc),
        new ForecastUnchanged(Utc),
        new PollFailed(Utc, TimeSpan.FromMinutes(5)),
        new PollDeferred(Utc, TimeSpan.FromSeconds(5)),
    ];

    [Fact]
    public void PersistedPollWorkerState_roundtrip()
    {
        var result = PersistenceRoundtripHelper.Roundtrip(Snapshot);

        Assert.Equal(Snapshot, result);
    }

    [Fact]
    public void An_unstarted_PersistedPollWorkerState_roundtrip()
    {
        var empty = new PersistedPollWorkerState(null, false, TimeSpan.Zero, null, null, null, DateTimeOffset.MinValue, 0, PersistedPollPhase.Discovery, null, 0, null);

        var result = PersistenceRoundtripHelper.Roundtrip(empty);

        Assert.Equal(empty, result);
    }

    [Fact]
    public void An_old_format_document_with_a_numeric_phase_deserializes_into_the_same_state()
    {
        const string oldFormat = """
{
  "Location": "lucerne",
  "Active": true,
  "DiscoveryInterval": "00:20:00",
  "LastHash": 42,
  "LastChangeUtc": "2026-07-15T10:00:00+00:00",
  "PrevChangeUtc": "2026-07-15T07:00:00+00:00",
  "NextPollUtc": "2026-07-15T13:01:00+00:00",
  "MissCount": 2,
  "Phase": 1,
  "Cycle": "03:00:00",
  "TransientFailureCount": 1
}
""";

        var result = JsonConvert.DeserializeObject<PersistedPollWorkerState>(oldFormat);

        Assert.Equal(Snapshot, result);
    }

    [Fact]
    public void Persisted_poll_phases_keep_their_stored_numbers()
    {
        Assert.Multiple(
            () => Assert.Equal(0, Convert.ToInt32(PersistedPollPhase.Discovery)),
            () => Assert.Equal(1, Convert.ToInt32(PersistedPollPhase.Steady)),
            () => Assert.Equal(2, Enum.GetValues<PersistedPollPhase>().Length));
    }

    [Theory]
    [MemberData(nameof(Events))]
    public void Every_poll_worker_event_roundtrips_with_its_type(object evt)
    {
        var result = PersistenceRoundtripHelper.Roundtrip(evt);

        Assert.Multiple(
            () => Assert.IsType(evt.GetType(), result),
            () => Assert.Equal(evt, result));
    }

    [Fact]
    public Task PersistedPollWorkerState_wire_format() =>
        Verify(JsonConvert.SerializeObject(Snapshot, Formatting.Indented));

    [Fact]
    public Task Poll_worker_events_wire_format() =>
        Verify(string.Join("\n", Events().Select(row => JsonConvert.SerializeObject(row.Data, Formatting.Indented))));
}
