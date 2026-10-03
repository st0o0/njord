using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Njord.Actors;
using Njord.Configuration;
using Njord.Tests.Shared;

namespace Njord.Tests.Configuration;

public sealed class ActorKeyRegistrationSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Njord:Locations:0:Name"] = "Lucerne",
                ["Njord:Locations:0:Latitude"] = "47.05",
                ["Njord:Locations:0:Longitude"] = "8.31",
                ["Njord:Models:0"] = "icon_d2",
                ["Njord:Mqtt:Enabled"] = "true",
                ["Njord:Mqtt:Host"] = "localhost",
            })
            .Build();
        new NjordServiceSetup().SetupServices(services, config);
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        NjordActorSystemSetup.WithNjordActors(builder.AddTestPersistence(), mqttEnabled: true);
    }

    public static TheoryData<Type> Markers() =>
    [
        typeof(ISchedulerActor),
        typeof(IBudgetTrackerActor),
        typeof(IPipelineActor),
        typeof(IEgressActor),
        typeof(IModelStateActor),
        typeof(IEnrichmentActor),
        typeof(ISensorHubActor),
        typeof(IForecastSnapshotActor),
        typeof(IEnrichmentSnapshotActor),
        typeof(IGrpcSnapshotConsumerActor),
        typeof(IMqttConnectionActor),
        typeof(IMqttEgressActor),
        typeof(IDiscoveryActor),
    ];

    [Theory(Timeout = TestTimeouts.Hosted)]
    [MemberData(nameof(Markers))]
    public async Task Registry_resolves_every_actor_marker(Type marker)
    {
        await AwaitAssertAsync(
            () =>
            {
                var found = ActorRegistry.TryGet(marker, out var actor);

                Assert.True(found, $"{marker.Name} is not registered");
                Assert.NotNull(actor);
                Assert.NotEqual(ActorRefs.Nobody, actor);
            },
            duration: TestTimeouts.AwaitAssertMax,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
