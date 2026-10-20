using Akka.Actor;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerOutcomeSpec : PollWorkerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_first_start_reports_the_initial_state_and_polls_immediately()
    {
        var ct = TestContext.Current.CancellationToken;
        CreateWorker().Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var request = await ExpectRequestAsync(ct);

        Assert.Multiple(
            () => Assert.Equal(Target, report.Target),
            () => Assert.Equal(PollPhase.Discovery, report.State.Phase),
            () => Assert.Equal(_time.GetUtcNow(), report.State.NextPollUtc),
            () => Assert.Null(report.State.LastChangeUtc),
            () => Assert.Equal(Lucerne, request.Location),
            () => Assert.Equal(DomainKind.Weather, request.Domain));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_changed_hash_is_reported_with_the_next_discovery_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(Completed(42), ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow(), report.State.LastChangeUtc),
            () => Assert.Equal(_time.GetUtcNow() + Discovery, report.State.NextPollUtc),
            () => Assert.Equal(0, report.State.MissCount));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_unchanged_hash_is_reported_as_a_miss()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        var report = await AnswerAllAndLastReportAsync(Completed(42), ct);

        Assert.Multiple(
            () => Assert.Equal(1, report.State.MissCount),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }

    // Per-domain cycle learning needs three consistent changes (two consistent intervals).
    // After the second change the domain stays in Discovery with a candidate cycle.
    // After the third consistent change it transitions to Steady.
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Three_consistent_changes_learn_the_cycle()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        // First change
        await AnswerAllAsync(Completed(1), ct);

        // Second change at +3h - records a candidate but stays Discovery
        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(Completed(2), ct);

        // Third consistent change at +6h - transitions to Steady
        _time.Advance(TimeSpan.FromHours(3));
        worker.Tell(new TriggerLocationPoll(TargetId));
        // Drain all requests and collect the last report to check the Weather domain state.
        var reports = new List<PollWorkerReport>();
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(ForDomain(Completed(3), request.Domain));
        }

        for (var i = 0; i < EnabledDomains.Count; i++)
        {
            reports.Add(await _manager.ExpectMsgAsync<PollWorkerReport>(TestTimeouts.AwaitAssertMax, cancellationToken: ct));
        }

        var third = reports[^1];
        Assert.Multiple(
            () => Assert.Equal(PollPhase.Steady, third.State.Phase),
            () => Assert.Equal(10800L, third.State.CycleSeconds));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_reply_spelled_differently_updates_the_configured_target()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(new FetchDomainCompleted(new Location("LUCERNE", 0, 0), DomainKind.Weather, 42), ct);

        Assert.Multiple(
            () => Assert.Equal(Target, report.Target),
            () => Assert.Equal(Lucerne, report.State.Location),
            () => Assert.NotNull(report.State.LastChangeUtc));
    }
}
