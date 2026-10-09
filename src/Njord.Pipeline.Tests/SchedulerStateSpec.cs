using Njord.Messages.Pipeline;
using Njord.Persistence;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

public sealed class SchedulerStateSpec
{
    private static readonly DateTimeOffset Now = TestTimeConstants.PipelineEpoch;
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(5);

    [Fact]
    public void Empty_state_has_no_entries()
    {
        var state = SchedulerState.Empty;
        Assert.Empty(state.States);
    }

    [Fact]
    public void Apply_adds_poll_state_for_new_key()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void Apply_updates_existing_entry_with_new_hash()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .Apply("lucerne|icon_d2", 99, Now.AddHours(1), DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(99, state.States["lucerne|icon_d2"].LastHash);
        Assert.Equal(PollPhase.Steady, state.States["lucerne|icon_d2"].Phase);
    }

    [Fact]
    public void Apply_tracks_multiple_models()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 1, Now, DiscoveryInterval)
            .Apply("zurich|gfs", 2, Now, DiscoveryInterval);

        Assert.Equal(2, state.States.Count);
    }

    [Fact]
    public void ApplyMiss_increments_miss_count()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .ApplyMiss("lucerne|icon_d2", Now.AddMinutes(10), DiscoveryInterval);

        Assert.Equal(1, state.States["lucerne|icon_d2"].MissCount);
    }

    [Fact]
    public void ApplyTransientFailure_increments_failure_count()
    {
        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now)
            .ApplyTransientFailure("lucerne|icon_d2", Now, DiscoveryInterval);

        Assert.Equal(1, state.States["lucerne|icon_d2"].TransientFailureCount);
    }

    [Fact]
    public void EnsureInitialized_does_not_overwrite_existing()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .EnsureInitialized("lucerne|icon_d2", Now.AddHours(1));

        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void EnsureInitialized_adds_new_entry()
    {
        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now);

        Assert.Single(state.States);
        Assert.Null(state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void GetSnapshot_returns_all_entries_as_poll_state_entries()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .Apply("zurich|gfs", 7, Now, DiscoveryInterval);

        var snapshot = state.GetSnapshot();

        Assert.Equal(2, snapshot.Entries.Count);
        Assert.Contains(snapshot.Entries, e => e.Location == "lucerne" && e.ModelId == "icon_d2");
        Assert.Contains(snapshot.Entries, e => e.Location == "zurich" && e.ModelId == "gfs");
    }

    [Fact]
    public void GetSnapshot_empty_state_returns_empty_entries()
    {
        var snapshot = SchedulerState.Empty.GetSnapshot();
        Assert.Empty(snapshot.Entries);
    }

    [Fact]
    public void ApplyRecover_restores_state_from_dto()
    {
        var dto = new DataChangedDto
        {
            Location = "lucerne",
            ModelId = "icon_d2",
            Hash = 42,
            UtcTicks = Now.UtcTicks,
        };

        var state = SchedulerState.Empty.ApplyRecover(dto, DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void SetPollState_replaces_entry()
    {
        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now);

        var modified = state.States["lucerne|icon_d2"] with { NextPollUtc = Now.AddHours(2) };
        state = state.SetPollState("lucerne|icon_d2", modified);

        Assert.Equal(Now.AddHours(2), state.States["lucerne|icon_d2"].NextPollUtc);
    }

    [Fact]
    public void ApplyHashResult_with_new_hash_decides_changed_and_records_hash()
    {
        var (state, decision) = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .ApplyHashResult("lucerne|icon_d2", 99, Now.AddHours(1), DiscoveryInterval);

        Assert.Equal(HashDecision.Changed, decision);
        Assert.Equal(99, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void ApplyHashResult_with_same_hash_decides_miss_and_counts_it()
    {
        var (state, decision) = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .ApplyHashResult("lucerne|icon_d2", 42, Now.AddHours(1), DiscoveryInterval);

        Assert.Equal(HashDecision.Miss, decision);
        Assert.Equal(1, state.States["lucerne|icon_d2"].MissCount);
    }

    [Fact]
    public void ApplyHashResult_for_unknown_key_decides_changed()
    {
        var (state, decision) = SchedulerState.Empty
            .ApplyHashResult("lucerne|icon_d2", 7, Now, DiscoveryInterval);

        Assert.Equal(HashDecision.Changed, decision);
        Assert.Equal(7, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact]
    public void ApplyRateLimited_raises_next_poll_to_the_minimum_delay()
    {
        var minDelay = TimeSpan.FromMinutes(5);

        var state = SchedulerState.Empty
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

        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now)
            .ApplyRateLimited("lucerne|icon_d2", Now, minDelay, DiscoveryInterval);

        Assert.Equal(Now.AddMinutes(1), state.States["lucerne|icon_d2"].NextPollUtc);
    }
}
