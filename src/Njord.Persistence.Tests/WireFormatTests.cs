using Newtonsoft.Json;
using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class WireFormatTests
{
    private static readonly DateTimeOffset _utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly PersistedPollWorkerState _state = new(
        "lucerne", true,
        [new PersistedDomainState(0, 42, PersistedPollPhase.Active, _utc, _utc.AddHours(3), 0, TimeSpan.FromMinutes(60))],
        47.05, 8.31);

    [Fact]
    public Task Poll_worker_state_shape() =>
        Verify(JsonConvert.SerializeObject(_state, Formatting.Indented));

    [Fact]
    public Task Poll_worker_event_shape() =>
        Verify(string.Join("\n", PollWorkerRoundtripTests.Events()
            .Select(row => JsonConvert.SerializeObject(row.Data, Formatting.Indented))));

    [Fact]
    public void Should_deserialize_a_domain_state_written_before_poll_interval()
    {
        const string oldFormat = """{ "Domain": 1, "LastHash": 7 }""";
        var recovered = JsonConvert.DeserializeObject<PersistedDomainState>(oldFormat);
        Assert.Equal(new PersistedDomainState(1, 7), recovered);
    }

    [Fact]
    public void Should_deserialize_a_worker_state_written_before_the_redesign()
    {
        const string oldFormat = """{ "Location": "lucerne", "Active": true }""";
        var recovered = JsonConvert.DeserializeObject<PersistedPollWorkerState>(oldFormat);
        Assert.NotNull(recovered);
        Assert.Multiple(
            () => Assert.Equal("lucerne", recovered.Location),
            () => Assert.Null(recovered.Latitude),
            () => Assert.Null(recovered.Longitude));
    }

    [Fact]
    public void Should_keep_the_stored_numbers_of_persisted_poll_phases()
    {
        Assert.Multiple(
            () => Assert.Equal(0, (int)PersistedPollPhase.Active),
            () => Assert.Equal(2, (int)PersistedPollPhase.Suspended),
            () => Assert.Equal(2, Enum.GetValues<PersistedPollPhase>().Length));
    }

    [Fact]
    public void Should_keep_the_stored_numbers_of_persisted_domain_poll_outcomes()
    {
        Assert.Multiple(
            () => Assert.Equal(0, (int)PersistedDomainPollOutcome.Changed),
            () => Assert.Equal(1, (int)PersistedDomainPollOutcome.Unchanged),
            () => Assert.Equal(2, (int)PersistedDomainPollOutcome.Failed),
            () => Assert.Equal(3, (int)PersistedDomainPollOutcome.NoData),
            () => Assert.Equal(4, Enum.GetValues<PersistedDomainPollOutcome>().Length));
    }
}
