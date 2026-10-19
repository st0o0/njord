using System.Collections.Immutable;
using Akka.Actor;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Pipeline.Tests;

public sealed class PollManagerStateSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly PollManagerConfig Config = PollManagerConfig.Create(
        [
            new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 },
            new LocationOptions { Name = "zurich", Latitude = 47.37, Longitude = 8.54 },
        ]);

    private static readonly PollTarget Lucerne = new(new LocationId("lucerne"));
    private static readonly PollTarget Zurich = new(new LocationId("zurich"));

    private static PollManagerState InitialState() =>
        PollManagerState.Create(Config, TimeSpan.FromMinutes(20), T0);

    [Fact]
    public void Create_initializes_all_targets_in_discovery_phase()
    {
        var state = InitialState();

        Assert.Equal(2, state.States.Count);
        Assert.All(state.States.Values, e => Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, e.Phase),
            () => Assert.Equal(T0, e.NextPollUtc)));
    }

    [Fact]
    public void ApplyReport_updates_the_target_state()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne.Location, PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var report = new PollWorkerReport(Lucerne, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.Equal(entry, state.States[Lucerne.Id]);
    }

    [Fact]
    public void ApplyReport_for_unknown_target_returns_unchanged_state()
    {
        var state = InitialState();
        var unknown = new PollTarget(new LocationId("atlantis"));
        var entry = new PollStateEntry(unknown.Location, PollPhase.Steady, T0, T0, 0, null);
        var report = new PollWorkerReport(unknown, PollWorkerOutcome.Changed, entry);

        var newState = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.Same(state.States, newState.States);
    }

    [Fact]
    public void ApplyReport_changed_updates_last_successful_poll()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne.Location, PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var report = new PollWorkerReport(Lucerne, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.Equal(T0, state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_unchanged_updates_last_successful_poll()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne.Location, PollPhase.Steady, T0.AddHours(1), null, 0, null);
        var report = new PollWorkerReport(Lucerne, PollWorkerOutcome.Unchanged, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.Equal(T0, state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_failed_does_not_update_last_successful_poll()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne.Location, PollPhase.Discovery, T0.AddHours(1), null, 1, null);
        var report = new PollWorkerReport(Lucerne, PollWorkerOutcome.Failed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.Null(state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_clears_restart_attempts_on_non_planned_outcome()
    {
        var state = InitialState() with
        {
            RestartAttempts = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal)
                .Add(Lucerne.Id, 3),
        };
        var entry = new PollStateEntry(Lucerne.Location, PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var report = new PollWorkerReport(Lucerne, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.False(state.RestartAttempts.ContainsKey(Lucerne.Id));
    }

    [Fact]
    public void ApplyReport_planned_does_not_clear_restart_attempts()
    {
        var state = InitialState() with
        {
            RestartAttempts = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal)
                .Add(Lucerne.Id, 2),
        };
        var entry = new PollStateEntry(Lucerne.Location, PollPhase.Discovery, T0.AddHours(1), null, 0, null);
        var report = new PollWorkerReport(Lucerne, PollWorkerOutcome.Planned, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0);

        Assert.Equal(2, state.RestartAttempts[Lucerne.Id]);
    }

    [Fact]
    public void ApplyWorkerTerminated_removes_watch_and_tracks_restart()
    {
        var probe = ActorRefs.Nobody;
        var state = InitialState() with
        {
            Watched = ImmutableDictionary<IActorRef, string>.Empty.Add(probe, Lucerne.Id),
        };

        state = state.ApplyWorkerTerminated(probe, out var targetId, out var attempts);

        Assert.Equal(Lucerne.Id, targetId);
        Assert.Equal(0, attempts);
        Assert.False(state.Watched.ContainsKey(probe));
        Assert.Equal(1, state.RestartAttempts[Lucerne.Id]);
    }

    [Fact]
    public void ApplyRegionResolved_sets_region_and_resets_attempts()
    {
        var state = InitialState() with { ResolveAttempts = 3 };

        state = state.ApplyRegionResolved(ActorRefs.Nobody);

        Assert.Equal(ActorRefs.Nobody, state.Region);
        Assert.Equal(0, state.ResolveAttempts);
    }

    [Fact]
    public void GetPollStates_returns_entries_in_target_order()
    {
        var state = InitialState();

        var result = state.GetPollStates(Config);

        Assert.Equal(Config.Targets.Count, result.Entries.Count);
        Assert.Equal(Config.Targets.Select(t => t.Id), result.Entries.Select(e => e.Location.Name));
    }

    [Fact]
    public void GetPollHealth_returns_started_and_last_successful()
    {
        var state = InitialState() with { LastSuccessfulPollUtc = T0.AddMinutes(5) };

        var health = state.GetPollHealth();

        Assert.Multiple(
            () => Assert.Equal(T0, health.StartedUtc),
            () => Assert.Equal(T0.AddMinutes(5), health.LastSuccessfulPollUtc));
    }
}
