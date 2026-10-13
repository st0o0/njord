using System.Text.Json.Nodes;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Mqtt;
using Njord.Messages.Pipeline;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Mqtt.Tests;

public sealed class MqttStateSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateMqttStateActor(
        NjordOptions? options = null, TimeProvider? timeProvider = null,
        IMqttTransport? transport = null)
    {
        options ??= DefaultOptions();
        timeProvider ??= TestTimeouts.AtEpoch(Anchor);
        transport ??= new RecordingTransport();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return Sys.ActorOf(Props.Create(() => new MqttStatePublisher(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new MqttOptions { BaseTopic = "njord" }),
            parameters,
            timeProvider,
            transport)));
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
    public async Task Should_publish_per_model_update_as_mqtt_messages()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        var forecast = CreateForecast("icon_d2");
        hub.Emit(new PipelineEvent.ForecastReceived(new LocationId("lucerne"), new WeatherModel("icon_d2"), forecast));

        await transport.WaitForMessage(m => m.Topic.StartsWith("njord/")).WaitAsync(TestContext.Current.CancellationToken);
        var sent = transport.Sent;
        Assert.NotEmpty(sent);
        Assert.All(sent, m => Assert.StartsWith("njord/", m.Topic));
        Assert.All(sent, m => Assert.True(m.Retain));
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_trend_analysis_result_to_trends_topic()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        var trends = new TrendResult(
            new LocationId("lucerne"),
            new Dictionary<string, ParameterTrend?>(),
            null,
            new PrecipTimingInfo(2, 5),
            new ExtremaTimingInfo(null, null),
            null,
            null);
        hub.Emit(new PipelineEvent.TrendComputed(trends, Anchor));

        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/trends").WaitAsync(TestContext.Current.CancellationToken);
        var sent = Assert.Single(transport.Sent);
        var payload = JsonNode.Parse(sent.Payload);
        Assert.NotNull(payload);
        Assert.Multiple(
            () => Assert.True(sent.Retain),
            () => Assert.Equal(2, payload["precip_starts"]!.GetValue<int>()),
            () => Assert.Equal(5, payload["precip_ends"]!.GetValue<int>()));
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_consensus_snapshot_per_horizon_topic()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var horizon = new HorizonConsensus(
            21.5, 21.4, 1.0, 0.5, 0.9, null, null, [new WeatherModel("icon_d2"), new WeatherModel("icon_eu")]);
        var snapshot = new ConsensusSnapshot(
            new LocationId("lucerne"),
            new HourlyConsensus([new ParameterConsensus(temp, new Dictionary<Horizon, HorizonConsensus> { [Horizon.Hours(3)] = horizon })], 72),
            new DailyConsensus([], 4),
            Anchor);
        hub.Emit(new PipelineEvent.ConsensusComputed(snapshot, Anchor));

        await transport.WaitForMessage(m => m.Topic == "njord/lucerne/consensus/h3").WaitAsync(TestContext.Current.CancellationToken);
        var sent = Assert.Single(transport.Sent);
        var payload = JsonNode.Parse(sent.Payload);
        Assert.NotNull(payload);
        Assert.Multiple(
            () => Assert.Equal(21.5, payload[temp.JsonKey]!.GetValue<double>()),
            () => Assert.Equal(2, payload[$"{temp.JsonKey}_models"]!.GetValue<int>()));
    }

    internal sealed record SentMessage(string Topic, string Payload, bool Retain);

    internal sealed class RecordingTransport : IMqttTransport
    {
        private readonly List<SentMessage> _sent = [];
        private readonly List<(Func<SentMessage, bool> Predicate, TaskCompletionSource Tcs)> _waiters = [];
        private readonly object _lock = new();

        public IReadOnlyList<SentMessage> Sent { get { lock (_lock) { return [.. _sent]; } } }

        public Task SendAsync(string topic, string payload, bool retain, CancellationToken cancellationToken)
        {
            var msg = new SentMessage(topic, payload, retain);
            lock (_lock)
            {
                _sent.Add(msg);
                for (var i = _waiters.Count - 1; i >= 0; i--)
                {
                    if (_waiters[i].Predicate(msg))
                    {
                        _waiters[i].Tcs.TrySetResult();
                        _waiters.RemoveAt(i);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task WaitForMessage(Func<SentMessage, bool> predicate)
        {
            lock (_lock)
            {
                if (_sent.Any(predicate))
                    return Task.CompletedTask;

                var tcs = new TaskCompletionSource();
                _waiters.Add((predicate, tcs));
                return tcs.Task;
            }
        }
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

    private static Forecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new Forecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }
}
