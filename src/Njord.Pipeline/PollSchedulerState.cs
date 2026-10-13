using System.Collections.Immutable;
using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Pipeline;

namespace Njord.Pipeline;

public sealed record PollSchedulerState(ImmutableDictionary<string, ModelPollState> States)
{
    public static readonly PollSchedulerState Empty = new(ImmutableDictionary<string, ModelPollState>.Empty);

    public static PollSchedulerState FromPersistence(PersistedPollSchedulerState dto, DateTimeOffset now)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, ModelPollState>();
        foreach (var entry in dto.Entries)
        {
            var lastChange = entry.LastChangeUtcTicks is not null
                ? new DateTimeOffset(entry.LastChangeUtcTicks.Value, TimeSpan.Zero)
                : (DateTimeOffset?)null;
            var prevChange = entry.PrevChangeUtcTicks is not null
                ? new DateTimeOffset(entry.PrevChangeUtcTicks.Value, TimeSpan.Zero)
                : (DateTimeOffset?)null;
            var cycle = entry.CycleTicks is not null
                ? TimeSpan.FromTicks(entry.CycleTicks.Value)
                : (TimeSpan?)null;

            builder[entry.Key] = new ModelPollState(
                entry.LastHash,
                lastChange,
                prevChange,
                now,
                entry.MissCount,
                (PollPhase)entry.Phase,
                cycle,
                entry.TransientFailureCount);
        }

        return new PollSchedulerState(builder.ToImmutable());
    }
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
        DataChanged evt,
        TimeSpan discoveryInterval)
    {
        var key = $"{evt.Location}|{evt.ModelId}";
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(evt.Utc));
        var updated = pollState.WithDataChange(evt.Hash, evt.Utc, discoveryInterval);
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
        DataChanged evt,
        TimeSpan discoveryInterval)
    {
        var key = $"{evt.Location}|{evt.ModelId}";
        var pollState = state.States.GetValueOrDefault(key, ModelPollState.Initial(evt.Utc));
        return pollState.LastHash != evt.Hash
            ? (state.Apply(evt, discoveryInterval), HashDecision.Changed)
            : (state.ApplyMiss(key, evt.Utc, discoveryInterval), HashDecision.Miss);
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

    public static PersistedPollSchedulerState GetPersistenceState(this PollSchedulerState state) => new()
    {
        Entries = state.States.Select(kvp =>
        {
            var s = kvp.Value;
            return new PersistedModelPollEntry
            {
                Key = kvp.Key,
                LastHash = s.LastHash,
                LastChangeUtcTicks = s.LastChangeUtc?.UtcTicks,
                PrevChangeUtcTicks = s.PrevChangeUtc?.UtcTicks,
                MissCount = s.MissCount,
                Phase = (int)s.Phase,
                CycleTicks = s.Cycle?.Ticks,
                TransientFailureCount = s.TransientFailureCount,
            };
        }).ToArray(),
    };

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
        return state.Apply(evt, discoveryInterval);
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
