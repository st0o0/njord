namespace Njord.Pipeline.Tests;

public sealed class PollCycleTrackerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] TwoModels = ["icon_d2", "ecmwf_ifs025"];
    private static readonly string[] ThreeModels = ["icon_d2", "ecmwf_ifs025", "gfs_seamless"];

    [Fact]
    public void A_failed_model_completes_the_cycle_with_its_failure_counted()
    {
        var (tracker, first) = PollCycleTracker.Empty.Record("lucerne", "icon_d2", TwoModels, PollCycleOutcome.Changed, T0);
        Assert.Null(first);

        var (final, summary) = tracker.Record("lucerne", "ecmwf_ifs025", TwoModels, PollCycleOutcome.Failed, T0.AddSeconds(8));

        Assert.Equal(new PollCycleSummary("lucerne", 2, 1, 1, TimeSpan.FromSeconds(8)), summary);
        Assert.Equal(0, final.OpenCycles);
    }

    [Fact]
    public void A_cycle_where_every_model_fails_still_completes()
    {
        var (tracker, first) = PollCycleTracker.Empty.Record("lucerne", "icon_d2", TwoModels, PollCycleOutcome.Failed, T0);
        Assert.Null(first);

        var (_, summary) = tracker.Record("lucerne", "ecmwf_ifs025", TwoModels, PollCycleOutcome.Failed, T0.AddSeconds(1));

        Assert.Equal(new PollCycleSummary("lucerne", 2, 0, 2, TimeSpan.FromSeconds(1)), summary);
    }

    [Fact]
    public void Consecutive_cycles_with_a_failure_stay_separate()
    {
        var tracker = PollCycleTracker.Empty;
        var summaries = new List<PollCycleSummary?>();

        (tracker, var s) = tracker.Record("lucerne", "icon_d2", ThreeModels, PollCycleOutcome.Changed, T0);
        summaries.Add(s);
        (tracker, s) = tracker.Record("lucerne", "ecmwf_ifs025", ThreeModels, PollCycleOutcome.Unchanged, T0.AddSeconds(1));
        summaries.Add(s);
        (tracker, s) = tracker.Record("lucerne", "gfs_seamless", ThreeModels, PollCycleOutcome.Failed, T0.AddSeconds(2));
        summaries.Add(s);
        (tracker, s) = tracker.Record("lucerne", "icon_d2", ThreeModels, PollCycleOutcome.Unchanged, T0.AddMinutes(20));
        summaries.Add(s);
        (tracker, s) = tracker.Record("lucerne", "ecmwf_ifs025", ThreeModels, PollCycleOutcome.Unchanged, T0.AddMinutes(20).AddSeconds(1));
        summaries.Add(s);
        (tracker, s) = tracker.Record("lucerne", "gfs_seamless", ThreeModels, PollCycleOutcome.Changed, T0.AddMinutes(20).AddSeconds(3));
        summaries.Add(s);

        Assert.Equal(
            [
                null, null, new PollCycleSummary("lucerne", 3, 1, 1, TimeSpan.FromSeconds(2)),
                null, null, new PollCycleSummary("lucerne", 3, 1, 0, TimeSpan.FromSeconds(3)),
            ],
            summaries);
        Assert.Equal(0, tracker.OpenCycles);
    }

    [Fact]
    public void A_retry_before_completion_replaces_the_failed_outcome()
    {
        var (tracker, _) = PollCycleTracker.Empty.Record("lucerne", "icon_d2", TwoModels, PollCycleOutcome.Failed, T0);
        (tracker, var retry) = tracker.Record("lucerne", "icon_d2", TwoModels, PollCycleOutcome.Changed, T0.AddMinutes(1));
        Assert.Null(retry);

        var (_, summary) = tracker.Record("lucerne", "ecmwf_ifs025", TwoModels, PollCycleOutcome.Unchanged, T0.AddMinutes(2));

        Assert.Equal(new PollCycleSummary("lucerne", 2, 1, 0, TimeSpan.FromMinutes(2)), summary);
    }

    [Fact]
    public void Model_ids_match_case_insensitively()
    {
        var (tracker, _) = PollCycleTracker.Empty.Record("lucerne", "ICON_D2", TwoModels, PollCycleOutcome.Changed, T0);

        var (_, summary) = tracker.Record("lucerne", "ecmwf_ifs025", TwoModels, PollCycleOutcome.Changed, T0);

        Assert.NotNull(summary);
        Assert.Equal(2, summary.Changed);
    }

    [Fact]
    public void An_unknown_model_opens_no_cycle()
    {
        var (tracker, summary) = PollCycleTracker.Empty.Record("lucerne", "not_a_model", TwoModels, PollCycleOutcome.Failed, T0);

        Assert.Null(summary);
        Assert.Equal(0, tracker.OpenCycles);
    }

    [Fact]
    public void A_location_without_models_opens_no_cycle()
    {
        var (tracker, summary) = PollCycleTracker.Empty.Record("nowhere", "icon_d2", [], PollCycleOutcome.Failed, T0);

        Assert.Null(summary);
        Assert.Equal(0, tracker.OpenCycles);
    }

    [Fact]
    public void Open_cycles_are_bounded_by_the_locations()
    {
        var tracker = PollCycleTracker.Empty;
        for (var round = 0; round < 10; round++)
        {
            (tracker, _) = tracker.Record("lucerne", "icon_d2", TwoModels, PollCycleOutcome.Failed, T0.AddMinutes(round));
            (tracker, _) = tracker.Record("zurich", "icon_d2", TwoModels, PollCycleOutcome.Failed, T0.AddMinutes(round));
        }

        Assert.Equal(2, tracker.OpenCycles);
    }
}
