using System.Collections.Immutable;
using Akka.Actor;
using Njord.Core;
using Njord.Messages.Polling;

namespace Njord.Polling.Tests;

public sealed class PollManagerStateTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly PollManagerConfig _config = PollManagerConfig.Create(
        [
            new Location("lucerne", 47.05, 8.31),
            new Location("zurich", 47.37, 8.54),
        ]);

    private static readonly PollTarget _lucerne = new(new Location("lucerne", 0, 0));
    private static readonly PollTarget _zurich = new(new Location("zurich", 0, 0));

    private static PollManagerState InitialState() =>
        PollManagerState.Create(_config, TimeSpan.FromMinutes(20), _t0);

    [Fact]
    public void Create_initializes_all_targets_in_discovery_phase()
    {
        var state = InitialState();

        Assert.Equal(2, state.States.Count);
        Assert.All(state.States.Values, e => Assert.Multiple(
            () => Assert.Equal(PollPhase.Discovery, e.Phase),
            () => Assert.Equal(_t0, e.NextPollUtc)));
    }

    [Fact]
    public void ApplyReport_updates_the_target_state()
    {
        var state = InitialState();
        var entry = new PollStateEntry(_lucerne.Location, PollPhase.Steady, _t0.AddHours(1), _t0, 0, 3600);
        var report = new PollWorkerReport(_lucerne, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, _config, _t0);

        Assert.Equal(entry, state.States[_lucerne.Id]);
    }

    [Fact]
    public void ApplyReport_for_unknown_target_returns_unchanged_state()
    {
        var state = InitialState();
        var unknown = new PollTarget(new Location("atlantis", 0, 0));
        var entry = new PollStateEntry(unknown.Location, PollPhase.Steady, _t0, _t0, 0, null);
        var report = new PollWorkerReport(unknown, PollWorkerOutcome.Changed, entry);

        var newState = state.ApplyReport(report, ActorRefs.Nobody, _config, _t0);

        Assert.Same(state.States, newState.States);
    }

    [Theory]
    [InlineData(PollWorkerOutcome.Changed, true)]
    [InlineData(PollWorkerOutcome.Unchanged, true)]
    [InlineData(PollWorkerOutcome.Failed, false)]
    public void A_report_updates_the_last_successful_poll_time_only_for_successful_outcomes(
        PollWorkerOutcome outcome, bool succeeds)
    {
        var state = InitialState();
        var entry = new PollStateEntry(_lucerne.Location, PollPhase.Steady, _t0.AddHours(1), null, 0, null);
        var report = new PollWorkerReport(_lucerne, outcome, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, _config, _t0);

        Assert.Equal(succeeds ? _t0 : null, state.LastSuccessfulPollUtc);
    }

    [Fact]
    public void ApplyReport_clears_restart_attempts_on_non_planned_outcome()
    {
        var state = InitialState() with
        {
            RestartAttempts = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal)
                .Add(_lucerne.Id, 3),
        };
        var entry = new PollStateEntry(_lucerne.Location, PollPhase.Steady, _t0.AddHours(1), _t0, 0, 3600);
        var report = new PollWorkerReport(_lucerne, PollWorkerOutcome.Changed, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, _config, _t0);

        Assert.False(state.RestartAttempts.ContainsKey(_lucerne.Id));
    }

    [Fact]
    public void ApplyReport_planned_does_not_clear_restart_attempts()
    {
        var state = InitialState() with
        {
            RestartAttempts = ImmutableDictionary.Create<string, int>(StringComparer.Ordinal)
                .Add(_lucerne.Id, 2),
        };
        var entry = new PollStateEntry(_lucerne.Location, PollPhase.Discovery, _t0.AddHours(1), null, 0, null);
        var report = new PollWorkerReport(_lucerne, PollWorkerOutcome.Planned, entry);

        state = state.ApplyReport(report, ActorRefs.Nobody, _config, _t0);

        Assert.Equal(2, state.RestartAttempts[_lucerne.Id]);
    }

    [Fact]
    public void ApplyWorkerTerminated_removes_watch_and_tracks_restart()
    {
        var probe = ActorRefs.Nobody;
        var state = InitialState() with
        {
            Watched = ImmutableDictionary<IActorRef, string>.Empty.Add(probe, _lucerne.Id),
        };

        state = state.ApplyWorkerTerminated(probe, out var targetId, out var attempts);

        Assert.Equal(_lucerne.Id, targetId);
        Assert.Equal(0, attempts);
        Assert.False(state.Watched.ContainsKey(probe));
        Assert.Equal(1, state.RestartAttempts[_lucerne.Id]);
    }

    [Fact]
    public void GetPollStates_returns_entries_in_target_order()
    {
        var state = InitialState();

        var result = state.GetPollStates(_config);

        Assert.Equal(_config.Targets.Count, result.Entries.Count);
        Assert.Equal(_config.Targets.Select(t => t.Id), result.Entries.Select(e => e.Location.Name));
    }

    [Fact]
    public void GetPollHealth_returns_started_and_last_successful()
    {
        var state = InitialState() with { LastSuccessfulPollUtc = _t0.AddMinutes(5) };

        var health = state.GetPollHealth();

        Assert.Multiple(
            () => Assert.Equal(_t0, health.StartedUtc),
            () => Assert.Equal(_t0.AddMinutes(5), health.LastSuccessfulPollUtc));
    }
}
