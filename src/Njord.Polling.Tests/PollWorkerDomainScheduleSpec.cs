using Akka.Actor;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerDomainScheduleSpec : PollWorkerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_restarted_worker_polls_only_the_domains_that_are_due()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        // Weather answers, AirQuality fails: weather waits a discovery interval, air quality retries after a minute.
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            _gateway.LastSender.Tell(request.Domain == DomainKind.Weather
                ? new FetchDomainCompleted(Lucerne, DomainKind.Weather, 1)
                : new FetchDomainFailed(Lucerne, request.Domain, PollFailureReason.Transport, "down"));
        }

        await DrainReportsAsync(EnabledDomains.Count, ct);
        await StopAsync(worker, ct);

        _time.Advance(TimeSpan.FromMinutes(2));
        CreateWorker();

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var next = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.AirQuality, next.Domain);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Learned_domain_cycles_survive_a_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);

        // Three changes at 1 h intervals to reach Steady (domain needs two consistent intervals).
        await AnswerAllAsync(Completed(0), ct);
        for (var hash = 1; hash <= 2; hash++)
        {
            _time.Advance(TimeSpan.FromHours(1));
            worker.Tell(new TriggerLocationPoll(TargetId));
            await AnswerAllAsync(Completed(hash), ct);
        }

        var expectedNext = _time.GetUtcNow() + TimeSpan.FromHours(1) - DomainPollStateExtensions.SteadyBuffer;
        await StopAsync(worker, ct);
        CreateWorker();

        var recovered = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(expectedNext, recovered.State.NextPollUtc);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), ct);
    }
}
