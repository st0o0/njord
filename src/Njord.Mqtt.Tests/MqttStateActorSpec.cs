using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Actors;
using Njord.Core.Configuration;
using Njord.Messages.Egress;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;
using Njord.Weather;

namespace Njord.Mqtt.Tests;

public sealed class MqttStateActorSpec : NjordActorSpec
{
    protected override bool UseTestPersistence => false;

    private static readonly DateTimeOffset Anchor = TestTimeConstants.NoonAnchor;

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
        Mqtt = new MqttOptions { BaseTopic = "njord" },
    };

    private IActorRef CreateMqttStateActor(
        NjordOptions? options = null, TimeProvider? timeProvider = null,
        IMqttTransport? transport = null)
    {
        options ??= DefaultOptions();
        timeProvider ??= TestTimeouts.AtEpoch(Anchor);
        transport ??= new RecordingTransport();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return Sys.ActorOf(Props.Create(() => new MqttStateActor(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(options.Mqtt),
            parameters,
            timeProvider,
            transport)));
    }

    private FakeSourceHub RegisterFakePipelineHub()
    {
        var mat = Sys.Materializer();
        var hub = new FakeSourceHub(mat);
        ActorRegistry.Register<IPipelineActor>(hub.Actor(Sys, typeof(RequestPipelineOutput)), overwrite: true);
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
        hub.Emit(new PipelineEvent.ModelForecastUpdate("lucerne", new WeatherModel("icon_d2"), forecast));

        await transport.WaitForMessage(m => m.Topic.StartsWith("njord/")).WaitAsync(TestContext.Current.CancellationToken);
        var sent = transport.Sent;
        Assert.NotEmpty(sent);
        Assert.All(sent, m => Assert.StartsWith("njord/", m.Topic));
        Assert.All(sent, m => Assert.True(m.Retain));
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_nothing_for_an_unknown_enrichment_type_name()
    {
        var hub = RegisterFakePipelineHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await hub.WaitForQueue();

        hub.Emit(new PipelineEvent.AnalysisResult("lucerne", "no-such-type", new object()));

        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(transport.Sent);
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
                        sr => new PipelineOutputResponse(msg.RequestId, sr),
                        _ => null!);
            });
        }
    }

    private static ModelForecast CreateForecast(string model = "icon_d2")
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var points = new List<ForecastPoint>
        {
            new(Anchor.AddHours(3), new Dictionary<ParameterDef, double?> { [temp] = 28.8 }),
        };
        return new ModelForecast(new WeatherModel(model), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }
}
