using System.Collections.Immutable;
using Akka.Actor;
using Njord.Messages.Polling;

namespace Njord.Polling;

internal static class PollManagerStateExtensions
{
    public static PollManagerState ApplyReport(
        this PollManagerState state, PollWorkerReport report, IActorRef sender,
        PollManagerConfig config, DateTimeOffset now)
    {
        if (!config.TargetsById.TryGetValue(report.Target.Id, out _))
        {
            return state;
        }

        var newStates = state.States.SetItem(report.Target.Id, report.State);
        var newWatched = !state.Watched.ContainsKey(sender) && !sender.IsNobody()
            ? state.Watched.SetItem(sender, report.Target.Id)
            : state.Watched;

        if (report.Outcome == PollWorkerOutcome.Planned)
        {
            return state with { States = newStates, Watched = newWatched };
        }

        var newRestarts = state.RestartAttempts.Remove(report.Target.Id);
        var newLastSuccessful = state.LastSuccessfulPollUtc;

        switch (report.Outcome)
        {
            case PollWorkerOutcome.Changed:
            case PollWorkerOutcome.Unchanged:
                newLastSuccessful = now;
                break;
        }

        return state with
        {
            States = newStates,
            Watched = newWatched,
            RestartAttempts = newRestarts,
            LastSuccessfulPollUtc = newLastSuccessful,
        };
    }

    public static PollManagerState ApplyWorkerTerminated(
        this PollManagerState state, IActorRef actor, out string? targetId, out int previousAttempts)
    {
        targetId = null;
        previousAttempts = 0;
        if (!state.Watched.TryGetValue(actor, out targetId))
        {
            return state;
        }

        var newWatched = state.Watched.Remove(actor);
        previousAttempts = state.RestartAttempts.GetValueOrDefault(targetId);
        var newRestarts = state.RestartAttempts.SetItem(targetId, previousAttempts + 1);
        return state with { Watched = newWatched, RestartAttempts = newRestarts };
    }

    public static PollManagerState ApplyRegionResolved(this PollManagerState state, IActorRef region) =>
        state with { Region = region, ResolveAttempts = 0 };

    public static PollManagerState ApplyResolveRetry(this PollManagerState state) =>
        state with { ResolveAttempts = state.ResolveAttempts + 1 };

    public static PollStatesResult GetPollStates(this PollManagerState state, PollManagerConfig config) =>
        new([.. config.Targets.Select(t => state.States[t.Id])]);

    public static PollHealthResult GetPollHealth(this PollManagerState state) =>
        new(state.StartedUtc, state.LastSuccessfulPollUtc);
}
