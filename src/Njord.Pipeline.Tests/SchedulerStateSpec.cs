using Njord.Messages.Pipeline;
using Njord.Persistence.Events.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class PollSchedulerStateSpec
{
    private static readonly DateTimeOffset Now = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(5);

    private static DataChanged Evt(string key, int hash, DateTimeOffset utc)
    {
        var parts = key.Split('|', 2);
        return new DataChanged(parts[0], parts[1], hash, utc);
    }

    [Fact]
    public void Empty_state_has_no_entries()
    {
        var state = PollSchedulerState.Empty;
        Assert.Empty(state.States);
    }

    [Fact]
    public void Apply_adds_poll_state_for_new_key()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void Apply_updates_existing_entry_with_new_hash()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .Apply(Evt("lucerne|icon_d2", 99, Now.AddHours(1)), DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(99, state.States["lucerne|icon_d2"].LastHash);
        Assert.Equal(PollPhase.Steady, state.States["lucerne|icon_d2"].Phase);
    }

    [Fact]
    public void Apply_tracks_multiple_models()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 1, Now), DiscoveryInterval)
            .Apply(Evt("zurich|gfs", 2, Now), DiscoveryInterval);

        Assert.Equal(2, state.States.Count);
    }

    [Fact]
    public void ApplyMiss_increments_miss_count()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .ApplyMiss("lucerne|icon_d2", Now.AddMinutes(10), DiscoveryInterval);

        Assert.Equal(1, state.States["lucerne|icon_d2"].MissCount);
    }

    [Fact]
    public void ApplyTransientFailure_increments_failure_count()
    {
        var state = PollSchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now)
            .ApplyTransientFailure("lucerne|icon_d2", Now, DiscoveryInterval);

        Assert.Equal(1, state.States["lucerne|icon_d2"].TransientFailureCount);
    }

    [Fact]
    public void EnsureInitialized_does_not_overwrite_existing()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .EnsureInitialized("lucerne|icon_d2", Now.AddHours(1));

        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void EnsureInitialized_adds_new_entry()
    {
        var state = PollSchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now);

        Assert.Single(state.States);
        Assert.Null(state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void GetSnapshot_returns_all_entries_as_poll_state_entries()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .Apply(Evt("zurich|gfs", 7, Now), DiscoveryInterval);

        var snapshot = state.GetSnapshot();

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Contains(snapshot.Entries, e => e is { Location: "lucerne", ModelId: "icon_d2" });
        Assert.Contains(snapshot.Entries, e => e is { Location: "zurich", ModelId: "gfs" });
    }

    [Fact]
    public void GetSnapshot_empty_state_returns_empty_entries()
    {
        var snapshot = PollSchedulerState.Empty.GetSnapshot();
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void ApplyRecover_restores_state_from_dto()
    {
        var dto = new PersistedDataChanged("lucerne", "icon_d2", 42, Now);

        var state = PollSchedulerState.Empty.ApplyRecover(dto, DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void SetPollState_replaces_entry()
    {
        var state = PollSchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now);

        var modified = state.States["lucerne|icon_d2"] with { NextPollUtc = Now.AddHours(2) };
        state = state.SetPollState("lucerne|icon_d2", modified);

        Assert.Equal(Now.AddHours(2), state.States["lucerne|icon_d2"].NextPollUtc);
    }

    [Fact]
    public void ApplyHashResult_with_new_hash_decides_changed_and_records_hash()
    {
        var (state, decision) = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .ApplyHashResult(Evt("lucerne|icon_d2", 99, Now.AddHours(1)), DiscoveryInterval);

        Assert.Equal(HashDecision.Changed, decision);
        Assert.Equal(99, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void ApplyHashResult_with_same_hash_decides_miss_and_counts_it()
    {
        var (state, decision) = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .ApplyHashResult(Evt("lucerne|icon_d2", 42, Now.AddHours(1)), DiscoveryInterval);

        Assert.Equal(HashDecision.Miss, decision);
        Assert.Equal(1, state.States["lucerne|icon_d2"].MissCount);
    }

    [Fact]
    public void ApplyHashResult_for_unknown_key_decides_changed()
    {
        var (state, decision) = PollSchedulerState.Empty
            .ApplyHashResult(Evt("lucerne|icon_d2", 7, Now), DiscoveryInterval);

        Assert.Equal(HashDecision.Changed, decision);
        Assert.Equal(7, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void ApplyRateLimited_raises_next_poll_to_the_minimum_delay()
    {
        var minDelay = TimeSpan.FromMinutes(5);

        var state = PollSchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now)
            .ApplyRateLimited("lucerne|icon_d2", Now, minDelay, DiscoveryInterval);

        var poll = state.States["lucerne|icon_d2"];
        Assert.Equal(Now + minDelay, poll.NextPollUtc);
        Assert.Equal(1, poll.TransientFailureCount);
    }

    [Fact]
    public void ApplyRateLimited_keeps_a_later_backoff_untouched()
    {
        var minDelay = TimeSpan.FromSeconds(30);

        var state = PollSchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now)
            .ApplyRateLimited("lucerne|icon_d2", Now, minDelay, DiscoveryInterval);

        Assert.Equal(Now.AddMinutes(1), state.States["lucerne|icon_d2"].NextPollUtc);
    }

    [Fact]
    public void GetPersistenceState_and_FromPersistence_roundtrip()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval)
            .Apply(Evt("lucerne|ecmwf_ifs025", 99, Now.AddHours(1)), DiscoveryInterval);

        var dto = state.GetPersistenceState();
        var restored = PollSchedulerState.FromPersistence(dto, Now.AddHours(2));

        Assert.Equal(state.States.Count, restored.States.Count);

        foreach (var key in state.States.Keys)
        {
            var original = state.States[key];
            var recovered = restored.States[key];

            Assert.Multiple(
                () => Assert.Equal(original.LastHash, recovered.LastHash),
                () => Assert.Equal(original.LastChangeUtc, recovered.LastChangeUtc),
                () => Assert.Equal(original.PrevChangeUtc, recovered.PrevChangeUtc),
                () => Assert.Equal(original.MissCount, recovered.MissCount),
                () => Assert.Equal(original.Phase, recovered.Phase),
                () => Assert.Equal(original.Cycle, recovered.Cycle),
                () => Assert.Equal(original.TransientFailureCount, recovered.TransientFailureCount));
        }
    }

    [Fact]
    public void FromPersistence_sets_NextPollUtc_to_now()
    {
        var state = PollSchedulerState.Empty
            .Apply(Evt("lucerne|icon_d2", 42, Now), DiscoveryInterval);

        var dto = state.GetPersistenceState();
        var restoreTime = Now.AddHours(5);
        var restored = PollSchedulerState.FromPersistence(dto, restoreTime);

        Assert.Equal(restoreTime, restored.States["lucerne|icon_d2"].NextPollUtc);
    }
}
