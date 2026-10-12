using System.Collections.Immutable;
using Njord.Messages.Pipeline;
using PersistedDataChanged = Njord.Persistence.Events.Pipeline.DataChanged;

namespace Njord.Pipeline;

public sealed record PollSchedulerState(ImmutableDictionary<string, ModelPollState> States)
{
    public static readonly PollSchedulerState Empty = new(ImmutableDictionary<string, ModelPollState>.Empty);
}

public enum HashDecision
{
    Changed,
    Miss,
}

public static class PollSchedulerStateExtensions
{
    public static PollSchedulerState Apply(
        this PollSchedulerState state,
        string key,
        int hash,
        DateTimeOffset utc,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(utc));
        var updated = pollState.WithDataChange(hash, utc, discoveryInterval);
        return state with { States = state.States.SetItem(key, updated) };
    }

    public static PollSchedulerState ApplyMiss(
        this PollSchedulerState state,
        string key,
        DateTimeOffset now,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(now));
        var updated = pollState.WithMiss(now, discoveryInterval);
        return state with { States = state.States.SetItem(key, updated) };
    }

    public static PollSchedulerState ApplyTransientFailure(
        this PollSchedulerState state,
        string key,
        DateTimeOffset now,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(now));
        var updated = pollState.WithTransientFailure(now, discoveryInterval);
        return state with { States = state.States.SetItem(key, updated) };
    }

    public static (PollSchedulerState State, HashDecision Decision) ApplyHashResult(
        this PollSchedulerState state,
        string key,
        int hash,
        DateTimeOffset now,
        TimeSpan discoveryInterval)
    {
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(now));
        return pollState.LastHash != hash
            ? (state.Apply(key, hash, now, discoveryInterval), HashDecision.Changed)
            : (state.ApplyMiss(key, now, discoveryInterval), HashDecision.Miss);
    }

    public static PollSchedulerState ApplyRateLimited(
        this PollSchedulerState state,
        string key,
        DateTimeOffset now,
        TimeSpan minDelay,
        TimeSpan discoveryInterval)
    {
        var failed = state.ApplyTransientFailure(key, now, discoveryInterval);
        var pollState = failed.States[key];
        var floor = now + minDelay;
        return pollState.NextPollUtc < floor
            ? failed.SetPollState(key, pollState with { NextPollUtc = floor })
            : failed;
    }

    public static PollSchedulerState SetPollState(
        this PollSchedulerState state,
        string key,
        ModelPollState pollState) =>
        state with { States = state.States.SetItem(key, pollState) };

    public static PollSchedulerState EnsureInitialized(
        this PollSchedulerState state,
        string key,
        DateTimeOffset now)
    {
        if (state.States.ContainsKey(key))
        {
            return state;
        }

        return state with { States = state.States.SetItem(key, ModelPollState.Initial(now)) };
    }

    public static PollStatesResult GetSnapshot(this PollSchedulerState state)
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

    public static PollSchedulerState ApplyRecover(
        this PollSchedulerState state,
        PersistedDataChanged dto,
        TimeSpan discoveryInterval)
    {
        var evt = FromPersistedEvent(dto);
        var key = $"{evt.Location}|{evt.ModelId}";
        return state.Apply(key, evt.Hash, evt.Utc, discoveryInterval);
    }

    public static PersistedDataChanged ToPersistedEvent(DataChanged evt) => new()
    {
        Location = evt.Location,
        ModelId = evt.ModelId,
        Hash = evt.Hash,
        UtcTicks = evt.Utc.UtcTicks,
    };

    public static DataChanged FromPersistedEvent(PersistedDataChanged dto) =>
        new(dto.Location, dto.ModelId, dto.Hash, new DateTimeOffset(dto.UtcTicks, TimeSpan.Zero));
}
