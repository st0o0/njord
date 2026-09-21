using System.Collections.Immutable;
using Njord.Persistence;

namespace Njord.Pipeline;

public sealed record SchedulerState(ImmutableDictionary<string, ModelPollState> States)
{
    public static readonly SchedulerState Empty = new(ImmutableDictionary<string, ModelPollState>.Empty);
}

public static class SchedulerStateExtensions
{
    public static SchedulerState Apply(
        this SchedulerState state,
        string key,
        int hash,
        DateTimeOffset utc,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(utc));
        var updated = pollState.WithDataChange(hash, utc, discoveryInterval);
        return state with { States = state.States.SetItem(key, updated) };
    }

    public static SchedulerState ApplyMiss(
        this SchedulerState state,
        string key,
        DateTimeOffset now,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(now));
        var updated = pollState.WithMiss(now, discoveryInterval);
        return state with { States = state.States.SetItem(key, updated) };
    }

    public static SchedulerState ApplyTransientFailure(
        this SchedulerState state,
        string key,
        DateTimeOffset now,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(now));
        var updated = pollState.WithTransientFailure(now, discoveryInterval);
        return state with { States = state.States.SetItem(key, updated) };
    }

    public static SchedulerState SetPollState(
        this SchedulerState state,
        string key,
        ModelPollState pollState) =>
        state with { States = state.States.SetItem(key, pollState) };

    public static SchedulerState EnsureInitialized(
        this SchedulerState state,
        string key,
        DateTimeOffset now)
    {
        if (state.States.ContainsKey(key))
            return state;
        return state with { States = state.States.SetItem(key, ModelPollState.Initial(now)) };
    }

    public static PollStatesResult GetSnapshot(this SchedulerState state)
    {
        var entries = state.States.Select(kvp =>
        {
            var parts = kvp.Key.Split('|', 2);
            var s = kvp.Value;
            return new PollStateEntry(
                parts[0],
                parts[1],
                s.Phase,
                s.NextPollUtc,
                s.LastChangeUtc,
                s.MissCount,
                s.Cycle is not null ? (long)s.Cycle.Value.TotalSeconds : null);
        }).ToList();

        return new PollStatesResult(entries);
    }

    public static SchedulerState ApplyRecover(
        this SchedulerState state,
        DataChangedDto dto,
        TimeSpan discoveryInterval)
    {
        var evt = SchedulerDtoMapping.ToDomain(dto);
        var key = $"{evt.Location}|{evt.ModelId}";
        return state.Apply(key, evt.Hash, evt.Utc, discoveryInterval);
    }
}
