using Njord.Persistence;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class SchedulerStateSpec
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 6, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan DiscoveryInterval = TimeSpan.FromMinutes(5);

    [Fact(Timeout = 5000)]
    public void Empty_state_has_no_entries()
    {
        var state = SchedulerState.Empty;
        Assert.Empty(state.States);
    }

    [Fact(Timeout = 5000)]
    public void Apply_adds_poll_state_for_new_key()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact(Timeout = 5000)]
    public void Apply_updates_existing_entry_with_new_hash()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .Apply("lucerne|icon_d2", 99, Now.AddHours(1), DiscoveryInterval);

        Assert.Single(state.States);
        Assert.Equal(99, state.States["lucerne|icon_d2"].LastHash);
        Assert.Equal(PollPhase.Steady, state.States["lucerne|icon_d2"].Phase);
    }

    [Fact(Timeout = 5000)]
    public void Apply_tracks_multiple_models()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 1, Now, DiscoveryInterval)
            .Apply("zurich|gfs", 2, Now, DiscoveryInterval);

        Assert.Equal(2, state.States.Count);
    }

    [Fact(Timeout = 5000)]
    public void ApplyMiss_increments_miss_count()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .ApplyMiss("lucerne|icon_d2", Now.AddMinutes(10), DiscoveryInterval);

        Assert.Equal(1, state.States["lucerne|icon_d2"].MissCount);
    }

    [Fact(Timeout = 5000)]
    public void ApplyTransientFailure_increments_failure_count()
    {
        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now)
            .ApplyTransientFailure("lucerne|icon_d2", Now, DiscoveryInterval);

        Assert.Equal(1, state.States["lucerne|icon_d2"].TransientFailureCount);
    }

    [Fact(Timeout = 5000)]
    public void EnsureInitialized_does_not_overwrite_existing()
    {
        var state = SchedulerState.Empty
            .Apply("lucerne|icon_d2", 42, Now, DiscoveryInterval)
            .EnsureInitialized("lucerne|icon_d2", Now.AddHours(1));

        Assert.Equal(42, state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact(Timeout = 5000)]
    public void EnsureInitialized_adds_new_entry()
    {
        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now);

        Assert.Single(state.States);
        Assert.Null(state.States["lucerne|icon_d2"].LastHash);
    }

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
    public void GetSnapshot_empty_state_returns_empty_entries()
    {
        var snapshot = SchedulerState.Empty.GetSnapshot();
        Assert.Empty(snapshot.Entries);
    }

    [Fact(Timeout = 5000)]
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

    [Fact(Timeout = 5000)]
    public void SetPollState_replaces_entry()
    {
        var state = SchedulerState.Empty
            .EnsureInitialized("lucerne|icon_d2", Now);

        var modified = state.States["lucerne|icon_d2"] with { NextPollUtc = Now.AddHours(2) };
        state = state.SetPollState("lucerne|icon_d2", modified);

        Assert.Equal(Now.AddHours(2), state.States["lucerne|icon_d2"].NextPollUtc);
    }
}
