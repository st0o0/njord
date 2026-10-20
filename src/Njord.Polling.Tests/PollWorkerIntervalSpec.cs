using Akka.Actor;
using Njord.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerIntervalSpec : PollWorkerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_shorter_discovery_interval_replans_the_pending_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new StartPolling(Target, TimeSpan.FromMilliseconds(100)));

        await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        var next = await ExpectRequestAsync(ct);
        Assert.Equal(DomainKind.Weather, next.Domain);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_longer_discovery_interval_moves_the_next_poll_out()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new StartPolling(Target, TimeSpan.FromHours(2)));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromHours(2), report.State.NextPollUtc);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Without_a_change_the_next_discovery_poll_waits_for_the_configured_interval()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new StartPolling(Target, Discovery));

        var report = await ExpectReportAsync(PollWorkerOutcome.Planned, ct);
        Assert.Equal(_time.GetUtcNow() + Discovery, report.State.NextPollUtc);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromSeconds(1), ct);
    }
}
