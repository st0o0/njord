using System.Text.Json.Nodes;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Mqtt.Actors;
using Njord.Mqtt.Health;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttStateSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private static readonly Location Lucerne = DomainData.Lucerne;

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateMqttStateActor(
        NjordOptions? options = null, TimeProvider? timeProvider = null,
        IMqttTransport? transport = null, MqttSessionCounter? sessionCounter = null)
    {
        options ??= DefaultOptions();
        timeProvider ??= TestTimeouts.AtEpoch(Anchor);
        transport ??= new RecordingTransport();
        var parameters = ParameterSelection.Resolve(["Weather"], [], []);

        return Sys.ActorOf(Props.Create(() => new MqttStatePublisher(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new MqttOptions { BaseTopic = "njord" }),
            parameters,
            timeProvider,
            transport,
            MqttEgressTuning.Default,
            sessionCounter ?? new MqttSessionCounter())));
    }

    private FakeSourceHub RegisterFakePipelineHub()
    {
        var mat = Sys.Materializer();
        var hub = new FakeSourceHub(mat);
        ActorRegistry.Register<IForecastPipeline>(hub.Actor(Sys, typeof(RequestPipelineOutput)), overwrite: true);
        return hub;
    }

    [Fact(Timeout = 15000)]
    public async Task Should_request_pipeline_source_on_startup()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue().WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_weather_event_as_state_and_per_parameter_topics()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        hub.Emit(new PipelineEvent.WeatherDataReceived(Lucerne, DomainData.Weather(Anchor, hours: 3)));

        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/weather/temperature").WaitAsync(TestContext.Current.CancellationToken);
        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/weather/state").WaitAsync(TestContext.Current.CancellationToken);
        var temperature = Assert.Single(transport.Sent, m => m.Topic == "njord/lucerne/weather/temperature");
        var state = Assert.Single(transport.Sent, m => m.Topic == "njord/lucerne/weather/state");
        var payload = JsonNode.Parse(state.Payload);
        Assert.NotNull(payload);
        Assert.Multiple(
            () => Assert.Equal("10", temperature.Payload),
            () => Assert.True(temperature.Retain),
            () => Assert.True(state.Retain),
            () => Assert.Equal(10.0, payload["temperature"]!.GetValue<double>()),
            () => Assert.Equal(0.5, payload["ensemble_std_dev"]!.GetValue<double>()));
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_flood_event_to_the_flood_topics()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        hub.Emit(new PipelineEvent.FloodDataReceived(Lucerne, DomainData.Flood(Anchor, discharge: 12.5)));

        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/flood/discharge").WaitAsync(TestContext.Current.CancellationToken);
        var discharge = Assert.Single(transport.Sent, m => m.Topic == "njord/lucerne/flood/discharge");
        Assert.Equal("12.5", discharge.Payload);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_alert_raised_to_the_alert_topic()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        var alert = new Alert(AlertType.Frost, AlertSeverity.Yellow, 0.75, new FrostDetails(-2.1, null, 3),
            PeakValue: -3.5, HoursUntil: 4);
        hub.Emit(new PipelineEvent.AlertRaised(Lucerne, alert, Anchor));

        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/alerts/frost").WaitAsync(TestContext.Current.CancellationToken);
        var sent = Assert.Single(transport.Sent);
        var payload = JsonNode.Parse(sent.Payload);
        Assert.NotNull(payload);
        Assert.Multiple(
            () => Assert.True(sent.Retain),
            () => Assert.Equal("yellow", payload["severity"]!.GetValue<string>()),
            () => Assert.Equal(0.75, payload["confidence"]!.GetValue<double>()),
            () => Assert.Equal(-3.5, payload["peak_value"]!.GetValue<double>()),
            () => Assert.Equal(4, payload["hours_until"]!.GetValue<int>()));
    }

    [Fact(Timeout = 15000)]
    public async Task Should_ignore_retired_events()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        hub.Emit(EnrichmentEvents.Alerts(new LocationId("lucerne"), Anchor));
        hub.Emit(new PipelineEvent.FloodDataReceived(Lucerne, DomainData.Flood(Anchor)));

        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/flood/discharge").WaitAsync(TestContext.Current.CancellationToken);
        Assert.All(transport.Sent, m => Assert.StartsWith("njord/lucerne/flood/", m.Topic));
    }

    internal sealed class FakeSourceHub
    {
        private readonly IMaterializer _mat;
        private ISourceQueueWithComplete<PipelineEvent>? _queue;
        private readonly TaskCompletionSource _queueReady = new();

        public FakeSourceHub(IMaterializer mat) => _mat = mat;

        public IActorRef Actor(ActorSystem system, Type requestType)
            => system.ActorOf(Props.Create(() => new FakeSourceProvider(_mat, this, requestType)));

        public void Emit(PipelineEvent evt) => _queue?.OfferAsync(evt);

        public Task WaitForQueue() => _queueReady.Task;

        public Task Completion => _queue?.WatchCompletionAsync() ?? Task.CompletedTask;

        internal void SetQueue(ISourceQueueWithComplete<PipelineEvent> queue)
        {
            _queue = queue;
            _queueReady.TrySetResult();
        }
    }

    private sealed class FakeSourceProvider : ReceiveActor
    {
        public FakeSourceProvider(IMaterializer mat, FakeSourceHub hub, Type requestType)
        {
            Receive<RequestPipelineOutput>(msg =>
            {
                var (queue, source) = Source.Queue<PipelineEvent>(32, OverflowStrategy.DropHead)
                    .PreMaterialize(mat);
                hub.SetQueue(queue);

                source
                    .RunWith(StreamRefs.SourceRef<PipelineEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new RequestPipelineOutputCompleted(msg.RequestId, sr),
                        _ => null!);
            });
        }
    }
}
