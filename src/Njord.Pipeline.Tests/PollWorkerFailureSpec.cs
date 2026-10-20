using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Persistence;
using Akka.TestKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;

namespace Njord.Pipeline.Tests;

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

        await AnswerAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, reason, "simulated"), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);
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

        await AnswerAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, reason), ct);

        var report = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromSeconds(delaySeconds), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_pipeline_unavailable_rejection_retries_soon()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAsync(new FetchDomainFailed(Lucerne, DomainKind.Weather, PollFailureReason.Transport, "simulated"), ct);
        await ExpectReportAsync(PollWorkerOutcome.Failed, ct);

        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.PipelineUnavailable), ct);
        var deferred = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);

        // A subsequent failure gets a flat delay (no aggregate backoff counter in the per-domain model).
        worker.Tell(new TriggerLocationPoll(TargetId));
        await AnswerAsync(new FetchDomainRejected(Lucerne, DomainKind.Weather, FetchRejectionReason.QueueFull), ct);
        var queueFull = await ExpectReportAsync(PollWorkerOutcome.Failed, ct);

        Assert.Multiple(
            () => Assert.Equal(_time.GetUtcNow() + PollWorkerState.PipelineUnavailableRetry, deferred.State.NextPollUtc),
            () => Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), queueFull.State.NextPollUtc));
    }
}
