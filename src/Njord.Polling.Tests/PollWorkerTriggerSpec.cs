using Akka.Actor;
using Njord.Core;
using Njord.Core.Configuration.Polling;
using Njord.Forecast;
using Njord.Messages.Forecast;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerTriggerSpec : PollWorkerSpecBase
{
    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_poll_asks_only_the_configured_domains()
    {
        var ct = TestContext.Current.CancellationToken;
        IReadOnlySet<DomainKind> domains = new HashSet<DomainKind> { DomainKind.Weather, DomainKind.Flood };
        var worker = Sys.ActorOf(
            Props.Create(() => new PollWorker(_time, FetchTimeouts.Default, domains, TargetId)),
            $"worker-{Guid.NewGuid():N}");

        worker.Tell(new StartPolling(Target, Discovery));

        var first = await ExpectRequestAsync(ct);
        var second = await ExpectRequestAsync(ct);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        Assert.Equal([DomainKind.Weather, DomainKind.Flood], new[] { first.Domain, second.Domain }.Order());
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_trigger_during_a_request_in_flight_sends_no_second_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        var inFlight = new List<(IActorRef Sender, DomainKind Domain)>();
        foreach (var _ in EnabledDomains)
        {
            var request = await ExpectRequestAsync(ct);
            inFlight.Add((_gateway.LastSender, request.Domain));
        }

        worker.Tell(new TriggerLocationPoll(TargetId));
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);

        // The requests in flight still end normally and their outcome is reported per domain.
        foreach (var (sender, domain) in inFlight)
        {
            sender.Tell(ForDomain(Completed(42), domain));
        }

        await DrainReportsAsync(EnabledDomains.Count, ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_trigger_after_the_outcome_reaches_the_gateway()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await ExpectRequestAsync(ct);
        Assert.Equal(Target, new PollTarget(request.Location));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_replaced_gateway_is_used_by_the_next_poll()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = await StartedWorkerAsync(ct);
        await AnswerAllAsync(Completed(42), ct);

        var replacement = CreateTestProbe("gateway-replacement");
        ActorRegistry.Register<IForecastGateway>(replacement.Ref, overwrite: true);
        worker.Tell(new TriggerLocationPoll(TargetId));

        var request = await replacement.ExpectMsgAsync<FetchDomain>(TestTimeouts.AwaitAssertMax, cancellationToken: ct);
        Assert.Equal(DomainKind.Weather, request.Domain);
        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(200), ct);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_worker_that_was_never_started_ignores_triggers()
    {
        var ct = TestContext.Current.CancellationToken;
        var worker = CreateWorker();

        worker.Tell(new TriggerLocationPoll(TargetId));

        await _gateway.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(300), ct);
        await _manager.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(100), ct);
    }
}
