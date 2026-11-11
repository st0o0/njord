using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Budget;
using Njord.Messages.Polling;
using Njord.Messages.Results;
using Njord.Messages.Stream;
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

        var manifest = await service.GetManifest(new Proto.ManifestRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(["vienna_frost", "vienna_heat"], manifest.Entities.Select(e => e.EntityId).Order());
    }

    [Fact(Timeout = 30_000)]
    public async Task GetManifest_maps_entity_definition_fields()
    {
        var meta = new EntityMetadata(EntityPlatform.Sensor, "temperature", "mdi:thermometer", "°C");
        var service = CreateService(new RichTestRule("Frost", OutputKind.Alert, meta));

        var manifest = await service.GetManifest(new Proto.ManifestRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var entity = Assert.Single(manifest.Entities);
        Assert.Multiple(
            () => Assert.Equal("vienna_frost", entity.EntityId),
            () => Assert.Equal("Vienna", entity.Location),
            () => Assert.Equal("Frost", entity.Feature),
            () => Assert.Equal(Proto.EntityPlatform.Sensor, entity.Platform),
            () => Assert.Equal("temperature", entity.DeviceClass),
            () => Assert.Equal("°C", entity.Unit),
            () => Assert.Equal("mdi:thermometer", entity.Icon),
            () => Assert.Equal("forecast", entity.Category),
            () => Assert.Equal(Proto.OutputKind.Alert, entity.OutputKind));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_combines_budget_models_and_active_features()
    {
        var service = CreateService(new EntityTestRule("Heat"));

        var status = await service.GetStatus(new Proto.StatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        var model = Assert.Single(status.Models);
        Assert.Multiple(
            () => Assert.Equal(40, status.Budget.MonthlyUsed),
            () => Assert.Equal(4, status.Budget.DailyUsed),
            () => Assert.Equal(300_000, status.Budget.MonthlyLimit),
            () => Assert.Equal(10_000, status.Budget.DailyLimit),
            () => Assert.Equal(["Forecast:Heat"], status.ActiveEnrichments),
            () => Assert.Equal("Vienna", model.Location),
            () => Assert.Equal(Proto.PollPhase.Steady, model.Phase),
            () => Assert.False(status.BudgetPartial),
            () => Assert.False(status.ModelsPartial));
    }

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_enrichments_are_sorted()
    {
        var service = CreateService(
            new EntityTestRule("Zebra"),
            new EntityTestRule("Alpha"),
            new EntityTestRule("Mango"));

        var status = await service.GetStatus(new Proto.StatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Equal(["Forecast:Alpha", "Forecast:Mango", "Forecast:Zebra"], status.ActiveEnrichments);
    }

    [Fact(Timeout = 30_000)]
    public async Task GetStatus_includes_version_and_uptime()
    {
        var service = CreateService(new EntityTestRule("Heat"));

        var status = await service.GetStatus(new Proto.StatusRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.NotEmpty(status.Version),
            () => Assert.True(status.UptimeSeconds >= 0),
            () => Assert.NotNull(status.ProcessStart));
    }

    [Fact(Timeout = 30_000)]
    public async Task TriggerPoll_returns_count_and_targets()
    {
        var service = CreateService(new EntityTestRule("Heat"));

        var response = await service.TriggerPoll(new Proto.TriggerPollRequest(),
            TestServerCallContext.Create(TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.Equal(1, response.TriggeredCount),
            () => Assert.Equal(["Vienna"], response.Targets));
    }

    private NjordGrpcService CreateService(params IFeatureRule[] rules)
    {
        var registry = new EntityRegistry(
            rules,
            Options.Create(new NjordOptions { ResolvedLocations = [_vienna] }),
            Options.Create(new ActiveModels()));
        return new NjordGrpcService(ActorRegistry, Sys, registry, TimeProvider.System,
            TestServiceInfo.Opts(AppSettingsDefaults.Options<PollingOptions>()), rules, NullLogger<NjordGrpcService>.Instance);
    }

    private sealed class RichTestRule(string name, OutputKind output, EntityMetadata? meta) : IFeatureRule
    {
        public FeatureDescriptor Descriptor { get; } = new(output, FeatureCategory.Forecast, name, new DomainRequirements(), EntityMetadata: meta);
        public bool Enabled => true;
        public PipelineEvent? Execute(RingBuffer<LocationForecast> ring, DateTimeOffset now) => null;
    }

    private sealed class FakePollManager : ReceiveActor
    {
        public FakePollManager()
        {
            Receive<QueryPollStates>(_ => Sender.Tell(new PollStatesResult(
            [
                new PollStateEntry(_vienna, PollPhase.Steady, TestTimeConstants.BudgetEpoch, null, 0, null),
            ])));
            Receive<TriggerImmediatePoll>(_ => Sender.Tell(new TriggerImmediatePollCompleted(1, ["Vienna"])));
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
