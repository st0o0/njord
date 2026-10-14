using Newtonsoft.Json;
using Njord.Persistence.Events.Polling;
using static VerifyXunit.Verifier;

namespace Njord.Persistence.Tests;

public sealed class PollWorkerRecordsSpec
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly PersistedPollWorkerState Snapshot = new(
        "lucerne", "icon_d2", true, TimeSpan.FromMinutes(20), 42, Utc, Utc.AddHours(-3), Utc.AddHours(3).AddMinutes(1),
        2, 1, TimeSpan.FromHours(3), 1);

    public static TheoryData<object> Events() =>
    [
        new PollingStarted("lucerne", "icon_d2", TimeSpan.FromMinutes(20), Utc),
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
        var empty = new PersistedPollWorkerState(null, null, false, TimeSpan.Zero, null, null, null, DateTimeOffset.MinValue, 0, 0, null, 0);

        var result = PersistenceRoundtripHelper.Roundtrip(empty);

        Assert.Equal(empty, result);
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
