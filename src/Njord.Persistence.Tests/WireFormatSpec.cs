using Newtonsoft.Json;
using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class WireFormatSpec
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly PersistedPollWorkerState State = new(
        "lucerne", true, TimeSpan.FromMinutes(20),
        [new PersistedDomainState(0, 42, PersistedPollPhase.Steady, TimeSpan.FromHours(3), 2, Utc, TimeSpan.FromHours(3), Utc.AddHours(3))],
        47.05, 8.31);

    [Fact]
    public Task Poll_worker_state_shape() =>
        Verify(JsonConvert.SerializeObject(State, Formatting.Indented));

    [Fact]
    public Task Poll_worker_event_shape() =>
        Verify(string.Join("\n", PollWorkerRoundtripSpec.Events()
            .Select(row => JsonConvert.SerializeObject(row.Data, Formatting.Indented))));

    [Fact]
    public void Should_deserialize_a_domain_state_written_before_cycle_learning()
    {
        const string oldFormat = """{ "Domain": 1, "LastHash": 7 }""";
        var recovered = JsonConvert.DeserializeObject<PersistedDomainState>(oldFormat);
        Assert.Equal(new PersistedDomainState(1, 7), recovered);
    }

    [Fact]
    public void Should_deserialize_a_started_event_written_before_coordinates()
    {
        const string oldFormat = """{ "Location": "lucerne", "Domains": [0], "DiscoveryInterval": "00:20:00", "Utc": "2026-07-15T10:00:00+00:00" }""";
        var recovered = JsonConvert.DeserializeObject<PollingStarted>(oldFormat);
        Assert.NotNull(recovered);
        Assert.Multiple(
            () => Assert.Equal("lucerne", recovered.Location),
            () => Assert.Null(recovered.Latitude),
            () => Assert.Null(recovered.Longitude));
    }

    [Fact]
    public void Should_deserialize_a_worker_state_written_before_coordinates()
    {
        const string oldFormat = """{ "Location": "lucerne", "Active": true, "DiscoveryInterval": "00:20:00" }""";
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
            () => Assert.Equal(0, (int)PersistedPollPhase.Discovery),
            () => Assert.Equal(1, (int)PersistedPollPhase.Steady),
            () => Assert.Equal(2, Enum.GetValues<PersistedPollPhase>().Length));
    }

    [Fact]
    public void Should_keep_the_stored_numbers_of_persisted_domain_poll_outcomes()
    {
        Assert.Multiple(
            () => Assert.Equal(0, (int)PersistedDomainPollOutcome.Changed),
            () => Assert.Equal(1, (int)PersistedDomainPollOutcome.Unchanged),
            () => Assert.Equal(2, (int)PersistedDomainPollOutcome.Failed),
            () => Assert.Equal(3, Enum.GetValues<PersistedDomainPollOutcome>().Length));
    }
}
