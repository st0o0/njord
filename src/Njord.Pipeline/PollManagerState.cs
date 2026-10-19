using System.Collections.Immutable;
using Akka.Actor;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Pipeline;

internal sealed record PollManagerConfig(
    IReadOnlyList<PollTarget> Targets,
    IReadOnlyDictionary<string, PollTarget> TargetsById,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ModelsByLocation)
{
    public static PollManagerConfig Create(IList<LocationOptions> locations, IList<string> globalModels)
    {
        var targets = new List<PollTarget>();
        var targetsById = new Dictionary<string, PollTarget>(StringComparer.Ordinal);
        var modelsByLocation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in locations)
        {
            var models = globalModels.Union(location.Models ?? [], StringComparer.OrdinalIgnoreCase).ToList();
            modelsByLocation[location.Name] = models;
            foreach (var model in models)
            {
                var target = new PollTarget(new LocationId(location.Name), model);
                targets.Add(target);
                targetsById[target.Id] = target;
            }
        }

        return new PollManagerConfig(targets, targetsById, modelsByLocation);
    }
}

internal sealed record PollManagerState(
    ImmutableDictionary<string, PollStateEntry> States,
    ImmutableDictionary<IActorRef, string> Watched,
    ImmutableDictionary<string, int> RestartAttempts,
    PollCycleTracker Cycles,
    TimeSpan DiscoveryInterval,
    IActorRef? Region,
    int ResolveAttempts,
    DateTimeOffset StartedUtc,
    DateTimeOffset? LastSuccessfulPollUtc)
{
    public static PollManagerState Create(
        PollManagerConfig config, TimeSpan discoveryInterval, DateTimeOffset now)
    {
        var states = config.Targets.ToImmutableDictionary(
            t => t.Id,
            t => new PollStateEntry(t.Location, t.ModelId, PollPhase.Discovery, now, null, 0, null),
            StringComparer.Ordinal);

        return new PollManagerState(
            states,
            ImmutableDictionary<IActorRef, string>.Empty,
            ImmutableDictionary.Create<string, int>(StringComparer.Ordinal),
            PollCycleTracker.Empty,
            discoveryInterval,
            null,
            0,
            now,
            null);
    }
}

internal static class PollManagerStateExtensions
{
    public static PollManagerState ApplyReport(
        this PollManagerState state, PollWorkerReport report, IActorRef sender,
        PollManagerConfig config, DateTimeOffset now, out PollCycleSummary? cycleSummary)
    {
        cycleSummary = null;
        if (!config.TargetsById.TryGetValue(report.Target.Id, out var target))
        {
            return state;
        }

        var newStates = state.States.SetItem(target.Id, report.State);
        var newWatched = !state.Watched.ContainsKey(sender) && !sender.IsNobody()
            ? state.Watched.SetItem(sender, target.Id)
            : state.Watched;

        if (report.Outcome == PollWorkerOutcome.Planned)
        {
            return state with { States = newStates, Watched = newWatched };
        }

        var newRestarts = state.RestartAttempts.Remove(target.Id);
        var newLastSuccessful = state.LastSuccessfulPollUtc;
        var newCycles = state.Cycles;

        switch (report.Outcome)
        {
            case PollWorkerOutcome.Changed:
            case PollWorkerOutcome.Unchanged:
                newLastSuccessful = now;
                break;
        }

        if (report.Outcome is PollWorkerOutcome.Changed or PollWorkerOutcome.Unchanged or PollWorkerOutcome.Failed)
        {
            var cycleOutcome = report.Outcome switch
            {
                PollWorkerOutcome.Changed => PollCycleOutcome.Changed,
                PollWorkerOutcome.Unchanged => PollCycleOutcome.Unchanged,
                _ => PollCycleOutcome.Failed,
            };
            var resolvedModels = config.ModelsByLocation[target.Location.Name];
            (newCycles, cycleSummary) = newCycles.Record(target.Location.Name, target.ModelId, resolvedModels, cycleOutcome, now);
        }

        return state with
        {
            States = newStates,
            Watched = newWatched,
            RestartAttempts = newRestarts,
            Cycles = newCycles,
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

    public static PollManagerState ApplyPollingChanged(this PollManagerState state, TimeSpan discoveryInterval) =>
        state with { DiscoveryInterval = discoveryInterval };

    public static PollStatesResult GetPollStates(this PollManagerState state, PollManagerConfig config) =>
        new([.. config.Targets.Select(t => state.States[t.Id])]);

    public static PollHealthResult GetPollHealth(this PollManagerState state) =>
        new(state.StartedUtc, state.LastSuccessfulPollUtc);
}
