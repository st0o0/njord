using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Fetch;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Polling.Tests;

public sealed class PollWorkerFetchReplyTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly IOptions<DomainRequirements> Requirements =
        Options.Create(TestRequirements.For(DomainKind.Forecast, DomainKind.AirQuality));

    private static readonly PollTarget Target = new(new Location("lucerne", 47.05, 8.31));
    private readonly FakeTimeProvider _time = new(TestTimeConstants.PipelineEpoch);
    private TestProbe _manager = null!;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestPersistence();
        builder.WithActors((_, registry) =>
        {
            _manager = CreateTestProbe("manager");
            registry.Register<IPollManager>(_manager.Ref);
        });
    }

    [Fact(Timeout = 30_000)]
    public async Task A_missing_gateway_ends_in_a_transient_backoff()
    {
        var ct = TestContext.Current.CancellationToken;
        Sys.ActorOf(
                Props.Create(() => new PollWorker(Target.Id, _time, Options.Create(new PollingOptions()), Requirements)),
                $"worker-{Guid.NewGuid():N}")
            .Tell(new StartPolling(Target, TimeSpan.FromHours(1)));

        var planned = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.Equal(PollWorkerOutcome.Planned, planned.Outcome);

        PollWorkerReport? last = null;
        for (var i = 0; i < 2; i++)
        {
            last = await _manager.ExpectMsgAsync<PollWorkerReport>(TimeSpan.FromSeconds(5), cancellationToken: ct);
            Assert.Equal(PollWorkerOutcome.Failed, last.Outcome);
        }

        Assert.NotNull(last);
        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(1), last.State.NextPollUtc);
    }
}
