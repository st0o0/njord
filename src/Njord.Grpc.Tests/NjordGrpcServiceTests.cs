using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class NjordGrpcServiceTests : Akka.Hosting.TestKit.TestKit
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider) =>
        builder.WithActors((system, registry) =>
        {
            registry.Register<IPollManager>(system.ActorOf(Props.Create(() => new FakePollManager())));
            registry.Register<IBudgetTracker>(system.ActorOf(Props.Create(() => new FakeBudgetTracker())));
        });

    [Fact(Timeout = 30_000)]
    public async Task GetManifest_returns_an_entity_per_enabled_feature_and_location()
    {
        var service = CreateService(new EntityTestRule("Heat"), new EntityTestRule("Frost"), new EntityTestRule("Fog", enabled: false));

        var manifest = await service.GetManifest(new V3.ManifestRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(["vienna_frost", "vienna_heat"], manifest.Entities.Select(e => e.EntityId).Order());
    }

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_combines_budget_models_and_active_features()
    {
        var service = CreateService(new EntityTestRule("Heat"));

        var status = await service.GetStatus(new V3.StatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var model = Assert.Single(status.Models);
        Assert.Multiple(
            () => Assert.Equal(40, status.Budget.MonthlyUsed),
            () => Assert.Equal(["Forecast:Heat"], status.ActiveEnrichments),
            () => Assert.Equal("Vienna", model.Location),
            () => Assert.Equal(V3.PollPhase.Steady, model.Phase));
    }

    private NjordGrpcService CreateService(params EntityTestRule[] rules)
    {
        var manifest = new ManifestBuilder(rules, new NjordOptions { ResolvedLocations = [_vienna] }, new ActiveModels());
        return new NjordGrpcService(ActorRegistry, Sys, manifest, TimeProvider.System,
            TestServiceInfo.Opts(AppSettingsDefaults.Options<PollingOptions>()), rules, NullLogger<NjordGrpcService>.Instance);
    }

    private sealed class FakePollManager : ReceiveActor
    {
        public FakePollManager()
        {
            Receive<QueryPollStates>(_ => Sender.Tell(new PollStatesResult(
            [
                new PollStateEntry(_vienna, PollPhase.Steady, TestTimeConstants.BudgetEpoch, null, 0, null),
            ])));
        }
    }

    private sealed class FakeBudgetTracker : ReceiveActor
    {
        public FakeBudgetTracker()
        {
            Receive<QueryBudgetStatus>(_ => Sender.Tell(new BudgetStatusResult(40, 4, 300_000, 10_000)));
        }
    }
}
