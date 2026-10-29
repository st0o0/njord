using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Grpc.V2;
using Njord.Messages.Budget;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class OpsGrpcServiceTests : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder
            .WithActors((system, registry) =>
            {
                registry.Register<IPollManager>(system.ActorOf(Props.Create(() => new FakePollManager())));
                registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new FakeBudgetTracker())));
            });

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_combines_budget_and_model_poll_states()
    {
        var service = new OpsGrpcService(ActorRegistry, TimeProvider.System, TestServiceInfo.Opts(AppSettingsDefaults.Options<PollingOptions>()),
            [new TestServiceInfo.FloodRisk(), new TestServiceInfo.FloodRain(enabled: false), new TestServiceInfo.Bbq(), new TestServiceInfo.FrostAlert()],
            NullLogger<OpsGrpcService>.Instance);

        var status = await service.GetStatus(new GetStatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var model = Assert.Single(status.Models);
        Assert.Multiple(
            () => Assert.False(status.BudgetPartial),
            () => Assert.False(status.ModelsPartial),
            () => Assert.Equal(["Flood:FloodRisk", "Forecast:Frost", "Lifestyle:Bbq"], status.ActiveEnrichments),
            () => Assert.Equal(40, status.Budget.MonthlyUsed),
            () => Assert.Equal("Lucerne", model.Location),
            () => Assert.Equal(V2.PollPhase.Steady, model.Phase));
    }

    private sealed class FakePollManager : ReceiveActor
    {
        public FakePollManager() =>
            Receive<QueryPollStates>(_ => Sender.Tell(new PollStatesResult(
            [
                new PollStateEntry(new Location("Lucerne", 47.05, 8.31), Messages.Pipeline.PollPhase.Steady,
                    TestTimeConstants.BudgetEpoch, null, 0, null),
            ])));
    }

    private sealed class FakeBudgetTracker : ReceiveActor
    {
        public FakeBudgetTracker() =>
            Receive<QueryBudgetStatus>(_ => Sender.Tell(new BudgetStatusResult(40, 4, 300_000, 10_000)));
    }
}
