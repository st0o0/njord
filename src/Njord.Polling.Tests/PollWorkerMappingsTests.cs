using Newtonsoft.Json;
using Njord.Core;
using Njord.Core.Polling;
using Njord.Messages.Polling;
using Njord.Persistence.Events.Polling;
using static Njord.Polling.Tests.PollStateFixture;

namespace Njord.Polling.Tests;

public sealed class PollWorkerMappingsTests
{
    [Fact]
    public void ToPollStateEntry_describes_the_target_and_its_poll_state()
    {
        var entry = WithChanges().ToPollStateEntry();

        Assert.Multiple(
            () => Assert.Equal(Lucerne, entry.Location),
            () => Assert.Equal(PollPhase.Active, entry.Phase),
            () => Assert.Equal(T0.AddHours(6), entry.LastChangeUtc));
    }

    [Fact]
    public void ToPollStateEntry_reports_domain_interval()
    {
        var entry = Started().ToPollStateEntry();

        Assert.Multiple(
            () => Assert.Equal(PollPhase.Active, entry.Phase),
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
        var state = WithChanges().Apply(Unchanged(DomainKind.Forecast, T0.AddHours(9)));

        var restored = state.GetPersistenceState().FromPersistence();

        Assert.Multiple(
            () => Assert.Equal(state.Target, restored.Target),
            () => Assert.Equal(state.Active, restored.Active),
            () => Assert.Equal(state.Domains.Count, restored.Domains.Count),
            () => Assert.Equal(state.GetDomain(DomainKind.Forecast).PollInterval,
                restored.GetDomain(DomainKind.Forecast).PollInterval));
    }

    [Fact]
    public void GetPersistenceState_writes_the_domain_states()
    {
        var state = WithChanges();

        var dto = state.GetPersistenceState();

        Assert.NotNull(dto.DomainStates);
        Assert.Multiple(
            () => Assert.Equal("lucerne", dto.Location),
            () => Assert.True(dto.Active),
            () => Assert.Single(dto.DomainStates));

        var domainDto = dto.DomainStates[0];
        Assert.Multiple(
            () => Assert.Equal((int)DomainKind.Forecast, domainDto.Domain),
            () => Assert.Equal(3, domainDto.LastHash),
            () => Assert.Equal(PersistedPollPhase.Active, domainDto.Phase),
            () => Assert.Equal(TimeSpan.FromMinutes(60), domainDto.PollInterval));
    }

    [Fact]
    public void An_empty_state_survives_the_persistence_roundtrip()
    {
        var restored = PollWorkerState.Empty.GetPersistenceState().FromPersistence();

        Assert.Multiple(
            () => Assert.Null(restored.Target),
            () => Assert.False(restored.Active),
            () => Assert.Empty(restored.Domains));
    }

    [Fact]
    public void A_started_state_keeps_the_coordinates_through_the_persistence_roundtrip()
    {
        var state = PollWorkerState.Empty.Apply(
            new PollingStarted("lucerne", [(int)DomainKind.Forecast],
                IntervalsFor((DomainKind.Forecast, TimeSpan.FromMinutes(60))), T0, 47.05, 8.31));

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
    public void A_snapshot_without_coordinates_restores_the_zero_fallback()
    {
        var dto = new PersistedPollWorkerState("lucerne", true, null);

        var state = dto.FromPersistence();

        Assert.NotNull(state.Target);
        Assert.Multiple(
            () => Assert.Equal(0, state.Target.Location.Latitude),
            () => Assert.Equal(0, state.Target.Location.Longitude));
    }

    [Fact]
    public void A_domain_without_a_persisted_interval_uses_the_default()
    {
        var dto = new PersistedPollWorkerState("lucerne", true,
            [new PersistedDomainState((int)DomainKind.Marine, 7)]);

        var state = dto.FromPersistence();

        Assert.Equal(DomainDefaults.PollInterval(DomainKind.Marine), state.GetDomain(DomainKind.Marine).PollInterval);
    }

    [Fact]
    public void Mapper_produced_snapshot_survives_newtonsoft_roundtrip_with_type_name_handling_all()
    {
        var state = WithChanges().Apply(Unchanged(DomainKind.Forecast, T0.AddHours(9)));
        var dto = state.GetPersistenceState();

        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
        var json = JsonConvert.SerializeObject(dto, settings);
        var recovered = JsonConvert.DeserializeObject<PersistedPollWorkerState>(json, settings);

        Assert.NotNull(recovered);
        var recoveredDomains = Assert.IsType<List<PersistedDomainState>>(recovered.DomainStates);
        Assert.Multiple(
            () => Assert.Equal(dto.Location, recovered.Location),
            () => Assert.Equal(dto.Active, recovered.Active),
            () => Assert.Equal(dto.DomainStates!.Count, recoveredDomains.Count),
            () => Assert.Equal(dto.DomainStates[0], recoveredDomains[0]));
    }

    [Theory]
    [InlineData(PollPhase.Active, PersistedPollPhase.Active)]
    [InlineData(PollPhase.Suspended, PersistedPollPhase.Suspended)]
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

    [Fact]
    public void Legacy_steady_phase_maps_to_active()
    {
        Assert.Equal(PollPhase.Active, ((PersistedPollPhase)1).ToDomain());
    }
}
