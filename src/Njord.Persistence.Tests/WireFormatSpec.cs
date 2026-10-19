using Newtonsoft.Json;
using Njord.Persistence.Events.Polling;

namespace Njord.Persistence.Tests;

public sealed class WireFormatSpec
{
    private static readonly DateTimeOffset Utc = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly PersistedPollWorkerState State = new(
        "lucerne", true, TimeSpan.FromMinutes(20), 42, Utc, Utc.AddHours(-3), Utc.AddHours(3).AddMinutes(1),
        2, PersistedPollPhase.Steady, TimeSpan.FromHours(3), 1, null);

    [Fact]
    public Task Should_keep_the_poll_worker_state_wire_format_stable() =>
        Verify(JsonConvert.SerializeObject(State, Formatting.Indented));

    [Fact]
    public Task Should_keep_the_poll_worker_event_wire_format_stable() =>
        Verify(string.Join("\n", PollWorkerRoundtripSpec.Events()
            .Select(row => JsonConvert.SerializeObject(row.Data, Formatting.Indented))));

    [Fact]
    public void Should_deserialize_a_document_written_before_domain_states_existed()
    {
        const string oldFormat = """
            {
              "Location": "lucerne", "Active": true, "DiscoveryInterval": "00:20:00", "LastHash": 42,
              "LastChangeUtc": "2026-07-15T10:00:00+00:00", "PrevChangeUtc": "2026-07-15T07:00:00+00:00",
              "NextPollUtc": "2026-07-15T13:01:00+00:00", "MissCount": 2, "Phase": 1,
              "Cycle": "03:00:00", "TransientFailureCount": 1
            }
            """;

        var recovered = JsonConvert.DeserializeObject<PersistedPollWorkerState>(oldFormat);

        Assert.Equal(State, recovered);
    }

    [Fact]
    public void Should_keep_the_stored_numbers_of_persisted_poll_phases()
    {
        Assert.Multiple(
            () => Assert.Equal(0, (int)PersistedPollPhase.Discovery),
            () => Assert.Equal(1, (int)PersistedPollPhase.Steady),
            () => Assert.Equal(2, Enum.GetValues<PersistedPollPhase>().Length));
    }
}
