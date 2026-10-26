using Newtonsoft.Json;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using static Njord.Polling.Tests.PollStateFixture;

namespace Njord.Polling.Tests;

public sealed class PollWorkerMappingsTests
{
    [Fact]
    public void ToPollStateEntry_describes_the_target_and_its_poll_state()
    {
        var entry = Steady().ToPollStateEntry();

        Assert.Multiple(
            () => Assert.Equal(Lucerne, entry.Location),
            () => Assert.Equal(PollPhase.Steady, entry.Phase),
            () => Assert.Equal(T0.AddHours(6), entry.LastChangeUtc),
            () => Assert.Equal(0, entry.MissCount),
            () => Assert.Equal(10800L, entry.CycleSeconds));
    }

    [Fact]
    public void ToPollStateEntry_reports_a_null_cycle_in_discovery()
    {
        var entry = Started().ToPollStateEntry();

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, entry.Phase),
            () => Assert.Null(entry.CycleSeconds),
            () => Assert.Null(entry.LastChangeUtc));
    }

    [Fact]
    public void ToPollStateEntry_without_a_target_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => PollWorkerState.Empty.ToPollStateEntry());

        Assert.Equal("A poll worker without a target has no poll state", ex.Message);
    }

    [Fact]
    public void GetPersistenceState_and_FromPersistence_roundtrip()
    {
        var state = Steady().Apply(Unchanged(DomainKind.Weather, T0.AddHours(9)));

        var restored = state.GetPersistenceState().FromPersistence();

        Assert.Multiple(
            () => Assert.Equal(state.Target, restored.Target),
            () => Assert.Equal(state.Active, restored.Active),
            () => Assert.Equal(state.DiscoveryInterval, restored.DiscoveryInterval),
            () => Assert.Equal(state.Domains.Count, restored.Domains.Count),
            () => Assert.Equal(state.GetDomain(DomainKind.Weather), restored.GetDomain(DomainKind.Weather)));
    }

    [Fact]
    public void GetPersistenceState_writes_the_domain_states()
    {
        var state = Steady();

        var dto = state.GetPersistenceState();

        Assert.NotNull(dto.DomainStates);
        Assert.Multiple(
            () => Assert.Equal("lucerne", dto.Location),
            () => Assert.True(dto.Active),
            () => Assert.Equal(TimeSpan.FromMinutes(20), dto.DiscoveryInterval),
            () => Assert.Single(dto.DomainStates));

        var domainDto = dto.DomainStates[0];
        Assert.Multiple(
            () => Assert.Equal((int)DomainKind.Weather, domainDto.Domain),
            () => Assert.Equal(3, domainDto.LastHash),
            () => Assert.Equal(PersistedPollPhase.Steady, domainDto.Phase),
            () => Assert.Equal(TimeSpan.FromHours(3), domainDto.LearnedCycle));
    }

    [Fact]
    public void An_empty_state_survives_the_persistence_roundtrip()
    {
        var restored = PollWorkerState.Empty.GetPersistenceState().FromPersistence();

        Assert.Multiple(
            () => Assert.Null(restored.Target),
            () => Assert.False(restored.Active),
            () => Assert.Equal(TimeSpan.Zero, restored.DiscoveryInterval),
            () => Assert.Empty(restored.Domains));
    }

    [Fact]
    public void A_started_state_keeps_the_coordinates_through_the_persistence_roundtrip()
    {
        var state = PollWorkerState.Empty.Apply(
            new PollingStarted("lucerne", [(int)DomainKind.Weather], Discovery, T0, 47.05, 8.31));

        var dto = state.GetPersistenceState();
        var restored = dto.FromPersistence();

        Assert.NotNull(restored.Target);
        Assert.Multiple(
            () => Assert.Equal(47.05, dto.Latitude),
            () => Assert.Equal(8.31, dto.Longitude),
            () => Assert.Equal(47.05, restored.Target.Location.Latitude),
            () => Assert.Equal(8.31, restored.Target.Location.Longitude));
    }

    [Fact]
    public void A_started_event_without_coordinates_falls_back_to_zero()
    {
        var state = Started();

        Assert.NotNull(state.Target);
        Assert.Multiple(
            () => Assert.Equal(0, state.Target.Location.Latitude),
            () => Assert.Equal(0, state.Target.Location.Longitude));
    }

    [Fact]
    public void A_snapshot_without_coordinates_restores_the_zero_fallback()
    {
        var dto = new PersistedPollWorkerState("lucerne", true, Discovery, null);

        var state = dto.FromPersistence();

        Assert.NotNull(state.Target);
        Assert.Multiple(
            () => Assert.Equal(0, state.Target.Location.Latitude),
            () => Assert.Equal(0, state.Target.Location.Longitude));
    }

    [Fact]
    public void A_second_start_with_new_coordinates_replaces_the_target_and_keeps_the_domains()
    {
        var polled = Steady();
        var domainBefore = polled.GetDomain(DomainKind.Weather);

        var moved = polled.Apply(
            new PollingStarted("lucerne", [(int)DomainKind.Weather], Discovery, T0.AddHours(7), 47.10, 8.40));

        Assert.NotNull(moved.Target);
        Assert.Multiple(
            () => Assert.Equal(47.10, moved.Target.Location.Latitude),
            () => Assert.Equal(8.40, moved.Target.Location.Longitude),
            () => Assert.Equal(domainBefore, moved.GetDomain(DomainKind.Weather)));
    }

    [Fact]
    public void A_second_start_with_new_coordinates_and_interval_replaces_the_target()
    {
        var moved = Started().Apply(
            new PollingStarted("lucerne", [(int)DomainKind.Weather], TimeSpan.FromMinutes(30), T0, 47.10, 8.40));

        Assert.NotNull(moved.Target);
        Assert.Multiple(
            () => Assert.Equal(47.10, moved.Target.Location.Latitude),
            () => Assert.Equal(8.40, moved.Target.Location.Longitude));
    }

    [Fact]
    public void An_old_format_snapshot_with_a_numeric_phase_restores_a_steady_domain()
    {
        const string oldFormat = """
{
  "Location": "lucerne",
  "Active": true,
  "DiscoveryInterval": "00:20:00",
  "DomainStates": [
    {
      "Domain": 0,
      "LastHash": 2,
      "Phase": 1,
      "LearnedCycle": "03:00:00",
      "MissCount": 0,
      "LastChanged": "2026-07-15T10:00:00+00:00",
      "NextPollAt": "2026-07-15T13:00:00+00:00"
    }
  ]
}
""";

        var dto = JsonConvert.DeserializeObject<PersistedPollWorkerState>(oldFormat);

        Assert.NotNull(dto);
        var state = dto.FromPersistence();
        var weather = state.GetDomain(DomainKind.Weather);
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, weather.Phase),
            () => Assert.Equal(new PollTarget(Lucerne), state.Target),
            () => Assert.Equal(TimeSpan.FromHours(3), weather.LearnedCycle));
    }

    [Theory]
    [InlineData(PollPhase.Discovery, PersistedPollPhase.Discovery)]
    [InlineData(PollPhase.Steady, PersistedPollPhase.Steady)]
    public void Every_poll_phase_roundtrips_through_its_persisted_phase(PollPhase phase, PersistedPollPhase persisted)
    {
        Assert.Multiple(
            () => Assert.Equal(persisted, phase.ToPersistence()),
            () => Assert.Equal(phase, persisted.ToDomain()));
    }

    [Fact]
    public void Every_poll_phase_member_has_a_persisted_counterpart()
    {
        var phases = Enum.GetValues<PollPhase>();

        Assert.Equal(phases, phases.Select(p => p.ToPersistence().ToDomain()));
    }

    [Fact]
    public void An_undefined_poll_phase_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((PollPhase)99).ToPersistence());

        Assert.Equal("phase", ex.ParamName);
    }

    [Fact]
    public void An_undefined_persisted_poll_phase_throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ((PersistedPollPhase)99).ToDomain());

        Assert.Equal("phase", ex.ParamName);
    }
}
