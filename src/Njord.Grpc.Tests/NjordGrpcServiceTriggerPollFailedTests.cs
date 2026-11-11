using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using Njord.Messages.Results;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class NjordGrpcServiceTriggerPollFailedTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
        {
            registry.Register<IPollManager>(system.ActorOf(Props.Create(() => new FailingPollManager())));
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new FakeBudgetTracker())));
        });

    [Fact(Timeout = 30_000)]
    public async Task TriggerPoll_rethrows_on_failure()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TriggerPoll(new Proto.TriggerPollRequest(),
                TestServerCallContext.Create(TestContext.Current.CancellationToken)));
    }

    private NjordGrpcService CreateService()
    {
        IFeatureRule[] rules = [new EntityTestRule("Heat")];
        var registry = new EntityRegistry(
            rules,
            Options.Create(new NjordOptions { ResolvedLocations = [_vienna] }),
            Options.Create(new ActiveModels()));
        return new NjordGrpcService(ActorRegistry, Sys, registry, TimeProvider.System,
            TestServiceInfo.Opts(AppSettingsDefaults.Options<PollingOptions>()), rules, NullLogger<NjordGrpcService>.Instance);
    }

    private sealed class FailingPollManager : ReceiveActor
    {
        public FailingPollManager()
        {
            Receive<QueryPollStates>(_ => Sender.Tell(new PollStatesResult([])));
            Receive<TriggerImmediatePoll>(_ =>
                Sender.Tell(new TriggerImmediatePollFailed(new InvalidOperationException("test failure"))));
        }
    }

    private sealed class FakeBudgetTracker : ReceiveActor
    {
        public FakeBudgetTracker()
        {
            Receive<QueryBudgetStatus>(_ => Sender.Tell(new BudgetStatusResult(0, 0, 300_000, 10_000)));
        }
    }
}
