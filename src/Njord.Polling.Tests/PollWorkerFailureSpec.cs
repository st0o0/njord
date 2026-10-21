using Akka.Actor;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerFailureSpec : PollWorkerSpecBase
{
    [Theory(Timeout = TestTimeouts.Hosted)]
    [InlineData(PollFailureReason.Transport, 60)]
    [InlineData(PollFailureReason.ModelUnavailable, 60)]
    [InlineData(PollFailureReason.MalformedPayload, 60)]
    [InlineData(PollFailureReason.RateLimited, 300)]
    public async Task A_failure_is_reported_with_its_backoff(PollFailureReason reason, int delaySeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainFailed(Lucerne, DomainKind.Weather, reason, "simulated"), ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Theory(Timeout = TestTimeouts.Hosted)]
    [InlineData(FetchRejectionReason.Budget, 300)]
    [InlineData(FetchRejectionReason.QueueFull, 60)]
    [InlineData(FetchRejectionReason.Expired, 60)]
    [InlineData(FetchRejectionReason.PipelineUnavailable, 5)]
    public async Task A_rejection_is_reported_as_failed_with_its_backoff(FetchRejectionReason reason, int delaySeconds)
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainRejected(Lucerne, DomainKind.Weather, reason), ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pipeline_unavailable_rejection_retries_soon()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        var deferred = await AnswerAllAndLastReportAsync(
            new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.PipelineUnavailable), ct);

        // The pipeline-unavailable rejection leaves the failure count at one, so the next failure is the second and backs off two minutes.
        worker.Tell(new TriggerLocationPoll(TargetId));
        var queueFull = await AnswerAllAndLastReportAsync(
            new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.QueueFull), ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow() + PollWorkerState.PipelineUnavailableRetry, deferred.State.NextPollUtc),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(2), queueFull.State.NextPollUtc));
    }

    private async Task<PollWorkerReport> FailOnceAsync(IActorRef worker, CancellationToken ct)
    {
        worker.Tell(new TriggerLocationPoll(TargetId));
        return await AnswerAllAndLastReportAsync(
            new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_third_consecutive_failure_backs_off_four_minutes()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
        await FailOnceAsync(worker, ct);

        var third = await FailOnceAsync(worker, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(4), third.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_success_resets_the_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
        await FailOnceAsync(worker, ct);
        await FailOnceAsync(worker, ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(Completed(1), ct);
        var failed = await FailOnceAsync(worker, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(60), failed.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task The_backoff_is_kept_across_a_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
        for (var i = 0; i < 3; i++)
        {
            await FailOnceAsync(worker, ct);
        }

        await StopAsync(worker, ct);
        var recovered = CreateWorker();
        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);

        var next = await FailOnceAsync(recovered, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(16), next.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_rate_limit_after_several_failures_keeps_the_longer_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
        for (var i = 0; i < 4; i++)
        {
            await FailOnceAsync(worker, ct);
        }

        worker.Tell(new TriggerLocationPoll(TargetId));
        var report = await AnswerAllAndLastReportAsync(
            new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.RateLimited, "simulated"), ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(32), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pipeline_unavailable_rejection_does_not_grow_the_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.PipelineUnavailable), ct);
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAllAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.PipelineUnavailable), ct);

        var failed = await FailOnceAsync(worker, ct);

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(60), failed.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_failing_domain_does_not_disturb_the_weather_domain_state()
    {
        var ct = TestContext.Current.CancellationToken;
        await StartedWorkerAsync(ct);

        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(request.Domain == DomainKind.AirQuality
                ? new FetchDomainFailed(Lucerne, request.Domain, PollFailureReason.Transport, "simulated")
                : Completed(1));
        }

        // The report state follows the Weather domain (last change, miss count) and the earliest due domain (next poll).
        var last = await DrainReportsAsync(EnabledDomains.Count, ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow(), last.State.LastChangeUtc),
            () => Assert.Equal(0, last.State.MissCount),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(60), last.State.NextPollUtc));
    }
}
