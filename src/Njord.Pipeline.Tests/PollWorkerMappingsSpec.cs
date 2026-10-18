using Newtonsoft.Json;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Polling;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Pipeline.Tests;

public sealed class PollWorkerMappingsSpec
{
    private static readonly DateTimeOffset T0 = TestTimeConstants.PipelineEpoch;
    private static readonly LocationId Lucerne = new("lucerne");

    private static PollWorkerState Started() =>
        PollWorkerState.Empty.Apply(new PollingStarted("lucerne", "icon_d2", TimeSpan.FromMinutes(20), T0));

    private static PollWorkerState Steady() =>
        Started().Apply(new ForecastChanged(1, T0)).Apply(new ForecastChanged(2, T0.AddHours(3)));

    [Fact]
    public void ToPollStateEntry_describes_the_target_and_its_poll_state()
    {
        var entry = Steady().ToPollStateEntry();

        Assert.Multiple(
            () => Assert.Equal(Lucerne, entry.Location),
            () => Assert.Equal("icon_d2", entry.ModelId),
            () => Assert.Equal(PollPhase.Steady, entry.Phase),
            () => Assert.Equal(T0.AddHours(6).AddMinutes(1), entry.NextPollUtc),
            () => Assert.Equal(T0.AddHours(3), entry.LastChangeUtc),
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
        var state = Steady().Apply(new ForecastUnchanged(T0.AddHours(6))).Apply(new PollFailed(T0.AddHours(7), TimeSpan.Zero));

        var restored = state.GetPersistenceState().FromPersistence();

        Assert.Equal(state, restored);
    }

    [Fact]
    public void GetPersistenceState_writes_every_field_and_the_persisted_phase()
    {
        var state = Steady().Apply(new ForecastUnchanged(T0.AddHours(6)));

        var dto = state.GetPersistenceState();

        Assert.Equal(
            new PersistedPollWorkerState(
                "lucerne", "icon_d2", true, TimeSpan.FromMinutes(20), 2, T0.AddHours(3), T0, state.NextPollUtc,
                1, PersistedPollPhase.Steady, TimeSpan.FromHours(3), 0),
            dto);
    }

    [Fact]
    public void An_empty_state_survives_the_persistence_roundtrip()
    {
        var restored = PollWorkerState.Empty.GetPersistenceState().FromPersistence();

        Assert.Equal(PollWorkerState.Empty, restored);
    }

    [Fact]
    public void An_old_format_snapshot_with_a_numeric_phase_restores_a_steady_state()
    {
        const string oldFormat = """
{
  "Location": "lucerne",
  "ModelId": "icon_d2",
  "Active": true,
  "DiscoveryInterval": "00:20:00",
  "LastHash": 2,
  "LastChangeUtc": "2026-07-15T10:00:00+00:00",
  "PrevChangeUtc": "2026-07-15T07:00:00+00:00",
  "NextPollUtc": "2026-07-15T13:01:00+00:00",
  "MissCount": 0,
  "Phase": 1,
  "Cycle": "03:00:00",
  "TransientFailureCount": 0
}
""";

        var dto = JsonConvert.DeserializeObject<PersistedPollWorkerState>(oldFormat);

        Assert.NotNull(dto);
        var state = dto.FromPersistence();
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, state.Phase),
            () => Assert.Equal(new PollTarget(Lucerne, "icon_d2"), state.Target),
            () => Assert.Equal(TimeSpan.FromHours(3), state.Cycle));
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
