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
        ],
        ["icon_d2", "gfs_seamless"]);

    private static readonly PollTarget Lucerne_IconD2 = new(new LocationId("lucerne"), "icon_d2");
    private static readonly PollTarget Lucerne_Gfs = new(new LocationId("lucerne"), "gfs_seamless");
    private static readonly PollTarget Zurich_IconD2 = new(new LocationId("zurich"), "icon_d2");

    private static PollManagerState InitialState() =>
        PollManagerState.Create(Config, TimeSpan.FromMinutes(20), T0);

    [Fact]
    public void Create_initializes_all_targets_in_discovery_phase()
    {
        var state = InitialState();

        Assert.Equal(4, state.States.Count);
        Assert.All(state.States.Values, e => Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, e.Phase),
            () => Assert.Equal(T0, e.NextPollUtc)));
    }

    [Fact]
    public void ApplyReport_updates_the_target_state()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var report = new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out _);

        Assert.Equal(entry, state.States[Lucerne_IconD2.Id]);
    }

    [Fact]
    public void ApplyReport_for_unknown_target_returns_unchanged_state()
    {
        var state = InitialState();
        var unknown = new PollTarget(new LocationId("atlantis"), "icon_d2");
        var entry = new PollStateEntry(unknown.Location, "icon_d2", PollPhase.Steady, T0, T0, 0, null);
        var report = new PollWorkerReport(unknown, PollWorkerOutcome.Changed, entry);

        var newState = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out var summary);

        Assert.Same(state.States, newState.States);
        Assert.Null(summary);
    }

    [Fact]
    public void ApplyReport_changed_updates_last_successful_poll()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var report = new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out _);

        Assert.Equal(T0, state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_unchanged_updates_last_successful_poll()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Steady, T0.AddHours(1), null, 0, null);
        var report = new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Unchanged, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out _);

        Assert.Equal(T0, state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_failed_does_not_update_last_successful_poll()
    {
        var state = InitialState();
        var entry = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Discovery, T0.AddHours(1), null, 1, null);
        var report = new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Failed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out _);

        Assert.Null(state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_clears_restart_attempts_on_non_planned_outcome()
    {
        var state = InitialState() with
        {
            RestartAttempts = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal)
                .Add(Lucerne_IconD2.Id, 3),
        };
        var entry = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var report = new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out _);

        Assert.False(state.RestartAttempts.ContainsKey(Lucerne_IconD2.Id));
    }

    [Fact]
    public void ApplyReport_planned_does_not_clear_restart_attempts()
    {
        var state = InitialState() with
        {
            RestartAttempts = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal)
                .Add(Lucerne_IconD2.Id, 2),
        };
        var entry = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Discovery, T0.AddHours(1), null, 0, null);
        var report = new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Planned, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, Config, T0, out _);

        Assert.Equal(2, state.RestartAttempts[Lucerne_IconD2.Id]);
    }

    [Fact]
    public void ApplyReport_completes_a_cycle_when_all_models_reported()
    {
        var state = InitialState();
        var models = new[] { "icon_d2", "gfs_seamless" };
        var e1 = new PollStateEntry(Lucerne_IconD2.Location, "icon_d2", PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);
        var e2 = new PollStateEntry(Lucerne_Gfs.Location, "gfs_seamless", PollPhase.Steady, T0.AddHours(1), T0, 0, 3600);

        state = state.ApplyReport(new PollWorkerReport(Lucerne_IconD2, PollWorkerOutcome.Changed, e1), ActorRefs.Nobody, Config, T0, out var s1);
        state = state.ApplyReport(new PollWorkerReport(Lucerne_Gfs, PollWorkerOutcome.Unchanged, e2), ActorRefs.Nobody, Config, T0.AddSeconds(5), out var s2);

        Assert.Null(s1);
        Assert.NotNull(s2);
        Assert.Equal("lucerne", s2.Location);
        Assert.Equal(2, s2.Total);
        Assert.Equal(1, s2.Changed);
    }

    [Fact]
    public void ApplyWorkerTerminated_removes_watch_and_tracks_restart()
    {
        var probe = ActorRefs.Nobody;
        var state = InitialState() with
        {
            Watched = ImmutableDictionary<IActorRef, string>.Empty.Add(probe, Lucerne_IconD2.Id),
        };

        state = state.ApplyWorkerTerminated(probe, out var targetId, out var attempts);

        Assert.Equal(Lucerne_IconD2.Id, targetId);
        Assert.Equal(0, attempts);
        Assert.False(state.Watched.ContainsKey(probe));
        Assert.Equal(1, state.RestartAttempts[Lucerne_IconD2.Id]);
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
    public void ApplyPollingChanged_updates_discovery_interval()
    {
        var state = InitialState();
        var newInterval = TimeSpan.FromMinutes(5);

        state = state.ApplyPollingChanged(newInterval);

        Assert.Equal(newInterval, state.DiscoveryInterval);
    }

    [Fact]
    public void GetPollStates_returns_entries_in_target_order()
    {
        var state = InitialState();

        var result = state.GetPollStates(Config);

        Assert.Equal(Config.Targets.Count, result.Entries.Count);
        Assert.Equal(Config.Targets.Select(t => t.Id), result.Entries.Select(e => $"{e.Location.Name}|{e.ModelId}"));
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
