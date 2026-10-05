using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Njord.Actors;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Messages.Egress;
using Njord.Messages.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttDiscoveryActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor();
    }

    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ParameterDef WindSpeed = ParameterRegistry.GetByApiName("wind_speed_10m")!;

    private static NjordOptions DefaultOptions(bool discoveryEnabled = true) => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
        Mqtt = new MqttOptions { DiscoveryEnabled = discoveryEnabled },
        PollInterval = TimeSpan.FromSeconds(5),
    };

    private IActorRef CreateMqttDiscoveryActor(NjordOptions? options = null, IMqttTransport? transport = null, bool withPresenters = false)
    {
        options ??= DefaultOptions();
        transport ??= new MqttStateActorSpec.RecordingTransport();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        IEnumerable<IEnrichmentPresenter> presenters = withPresenters
            ? EnrichmentGoldenMasterFixtures.Presenters(options, parameters)
            : [];

        return Sys.ActorOf(Props.Create(() => new MqttDiscoveryActor(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(options.Mqtt),
            parameters,
            transport,
            presenters)));
    }

    private static EgressEvent.CapabilityLearned CreateCapability(
        string location = "lucerne",
        string modelId = "icon_d2",
        IReadOnlySet<ParameterDef>? supported = null)
    {
        supported ??= new HashSet<ParameterDef> { Temperature, WindSpeed };
        return new EgressEvent.CapabilityLearned(
            location,
            new WeatherModel(modelId),
            supported,
            [3, 6, 12, 24, 48],
            [0, 1]);
    }

    private MqttStateActorSpec.FakeSourceHub RegisterFakeModelStateHub()
    {
        var mat = Sys.Materializer();
        var hub = new MqttStateActorSpec.FakeSourceHub(mat);
        ActorRegistry.Register<IModelStateActor>(hub.Actor(Sys, typeof(RequestModelStateSource)), overwrite: true);
        return hub;
    }

    private void RegisterFakeConnectionForInbound(Akka.TestKit.TestProbe requestProbe)
    {
        var fake = Sys.ActorOf(Props.Create(() => new FakeConnectionForInbound(requestProbe)));
        ActorRegistry.Register<IMqttConnectionActor>(fake, overwrite: true);
    }

    [Fact(Timeout = 15000)]
    public async Task Discovery_disabled_does_not_request_source_or_subscribe()
    {
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        RegisterFakeModelStateHub();

        var options = DefaultOptions(discoveryEnabled: false);
        CreateMqttDiscoveryActor(options);

        await requestProbe.ExpectNoMsgAsync(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public async Task Subscribes_inbound_on_startup_when_enabled()
    {
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        RegisterFakeModelStateHub();

        CreateMqttDiscoveryActor();

        var msg = await requestProbe.ExpectMsgAsync<SubscribeInbound>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(msg);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_model_state_source_after_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestProbe = CreateTestProbe();
        ActorRegistry.Register<IModelStateActor>(Sys.ActorOf(FailingRefProvider.Props(requestProbe)), overwrite: true);
        ActorRegistry.Register<IMqttConnectionActor>(CreateTestProbe().Ref, overwrite: true);

        CreateMqttDiscoveryActor();

        await requestProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
        await requestProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
    }

    [Fact(Timeout = 15000)]
    public async Task No_discovery_published_on_connect_before_capabilities_arrive()
    {
        var transport = new MqttStateActorSpec.RecordingTransport();
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        RegisterFakeModelStateHub();

        var actor = CreateMqttDiscoveryActor(transport: transport);

        await requestProbe.ExpectMsgAsync<SubscribeInbound>(cancellationToken: TestContext.Current.CancellationToken);

        actor.Tell(new MqttConnected());

        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(transport.Sent);
    }

    [Fact(Timeout = 15000)]
    public async Task Publishes_discovery_after_all_capabilities_received()
    {
        var transport = new MqttStateActorSpec.RecordingTransport();
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        var hub = RegisterFakeModelStateHub();

        CreateMqttDiscoveryActor(transport: transport);

        await hub.WaitForQueue();

        hub.Emit(CreateCapability());

        await transport.WaitForMessage(m => m.Retain).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 10000)]
    public async Task Timeout_triggers_partial_discovery()
    {
        var transport = new MqttStateActorSpec.RecordingTransport();
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        var hub = RegisterFakeModelStateHub();

        var options = DefaultOptions();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        options.PollInterval = TimeSpan.FromMilliseconds(500);
        CreateMqttDiscoveryActor(options, transport: transport);

        await hub.WaitForQueue();

        hub.Emit(CreateCapability(modelId: "icon_d2"));

        await transport.WaitForMessage(m => m.Retain).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public async Task Ha_birth_re_publishes_with_learned_state()
    {
        var transport = new MqttStateActorSpec.RecordingTransport();
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        var hub = RegisterFakeModelStateHub();

        var actor = CreateMqttDiscoveryActor(transport: transport);

        await hub.WaitForQueue();

        hub.Emit(CreateCapability());

        await transport.WaitForMessage(m => m.Retain).WaitAsync(TestContext.Current.CancellationToken);

        // Clear and wait for quiescence
        await Task.Delay(300, TestContext.Current.CancellationToken);
        var countBefore = transport.Sent.Count;

        // Birth -> should re-publish
        actor.Tell(new MqttInboundMessage("homeassistant/status", "online"));

        await transport.WaitForMessage(m => transport.Sent.Count > countBefore).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public async Task Late_capability_triggers_incremental_publish()
    {
        var transport = new MqttStateActorSpec.RecordingTransport();
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        var hub = RegisterFakeModelStateHub();

        var options = DefaultOptions();
        options.Models = ["icon_d2", "ecmwf_ifs025"];
        CreateMqttDiscoveryActor(options, transport: transport);

        await hub.WaitForQueue();

        hub.Emit(CreateCapability(modelId: "icon_d2"));
        hub.Emit(CreateCapability(modelId: "ecmwf_ifs025"));

        // Wait until at least 2 messages sent
        await transport.WaitForMessage(_ => transport.Sent.Count >= 2).WaitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> PublishedDeviceIds(NjordOptions options, CancellationToken ct)
    {
        var transport = new MqttStateActorSpec.RecordingTransport();
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(requestProbe);
        var hub = RegisterFakeModelStateHub();

        CreateMqttDiscoveryActor(options, transport: transport, withPresenters: true);

        await hub.WaitForQueue();
        hub.Emit(CreateCapability());

        // Wait for 7 discovery messages (1 model + 6 enrichment presenters)
        await transport.WaitForMessage(_ => transport.Sent.Count >= 7).WaitAsync(ct);

        // Wait for quiescence
        await Task.Delay(300, ct);

        return transport.Sent
            .Select(m => m.Topic.Split('/')[^2])
            .ToList();
    }

    private static NjordOptions AllEnrichmentOptions(bool consensusEnabled)
    {
        var options = DefaultOptions();
        options.Enrichment = new EnrichmentOptions
        {
            Consensus = new ConsensusOptions { Enabled = consensusEnabled },
            Alerts = new AlertOptions { Enabled = true },
            Derived = new DerivedOptions { Enabled = true },
            Trends = new TrendOptions { Enabled = true },
            Indices = new IndexOptions { Enabled = true },
            History = new HistoryOptions { Enabled = true },
        };
        return options;
    }

    private static readonly string[] ExpectedDiscoveryOrder =
    [
        "njord_lucerne_icon_d2",
        "njord_lucerne_consensus",
        "njord_lucerne_alerts",
        "njord_lucerne_derived",
        "njord_lucerne_trends",
        "njord_lucerne_indices",
        "njord_lucerne_history",
    ];

    [Fact(Timeout = 15000)]
    public async Task Publishes_model_then_consensus_then_features_in_order_per_location()
    {
        var deviceIds = await PublishedDeviceIds(AllEnrichmentOptions(consensusEnabled: true), TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedDiscoveryOrder, deviceIds);
    }

    [Fact(Timeout = 15000)]
    public async Task Consensus_discovery_is_published_even_when_consensus_is_disabled()
    {
        var deviceIds = await PublishedDeviceIds(AllEnrichmentOptions(consensusEnabled: false), TestContext.Current.CancellationToken);

        Assert.Equal(ExpectedDiscoveryOrder, deviceIds);
    }

    [Fact(Timeout = 15000)]
    public async Task Re_requests_refs_after_model_state_actor_terminates()
    {
        var requestProbe = CreateTestProbe();
        RegisterFakeConnectionForInbound(CreateTestProbe());

        var mat = Sys.Materializer();
        var hub = new MqttStateActorSpec.FakeSourceHub(mat);
        var fakeModelState = hub.Actor(Sys, typeof(RequestModelStateSource));
        ActorRegistry.Register<IModelStateActor>(fakeModelState, overwrite: true);

        CreateMqttDiscoveryActor();

        await hub.WaitForQueue();

        Watch(fakeModelState);
        await fakeModelState.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(fakeModelState, cancellationToken: TestContext.Current.CancellationToken);

        var newHub = new MqttStateActorSpec.FakeSourceHub(mat);
        var newFake = newHub.Actor(Sys, typeof(RequestModelStateSource));
        ActorRegistry.Register<IModelStateActor>(newFake, overwrite: true);

        await newHub.WaitForQueue().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    // -- fake that accepts SubscribeInbound and forwards to probe ----

    private sealed class FakeConnectionForInbound : ReceiveActor
    {
        public FakeConnectionForInbound(IActorRef requestProbe)
        {
            Receive<SubscribeInbound>(msg => requestProbe.Tell(msg));
        }
    }
}
