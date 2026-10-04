using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Time.Testing;
using Njord.Actors;
using Njord.Analysis;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Messages.Egress;
using Njord.Mqtt;
using Njord.Mqtt.Transport;
using Njord.Tests.Shared;

namespace Njord.Mqtt.Tests;

public sealed class MqttStateActorSpec : Akka.Hosting.TestKit.TestKit
{
    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.AddTestTimefactor().AddFastRetryBackoff();
    }

    private static readonly DateTimeOffset Anchor = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
        Mqtt = new MqttOptions { BaseTopic = "njord" },
    };

    private IActorRef CreateMqttStateActor(
        NjordOptions? options = null, TimeProvider? timeProvider = null,
        IMqttTransport? transport = null, bool withPresenters = false)
    {
        options ??= DefaultOptions();
        timeProvider ??= new FakeTimeProvider(Anchor);
        transport ??= new RecordingTransport();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);
        IEnumerable<IEnrichmentPresenter> presenters = withPresenters
            ? EnrichmentGoldenMasterFixtures.Presenters(options, parameters)
            : [];

        return Sys.ActorOf(Props.Create(() => new MqttStateActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            timeProvider,
            transport,
            presenters)));
    }

    private FakeSourceHub RegisterFakeModelStateHub()
    {
        var mat = Sys.Materializer();
        var hub = new FakeSourceHub(mat);
        ActorRegistry.Register<IModelStateActor>(hub.Actor(Sys, typeof(RequestModelStateSource)), overwrite: true);
        return hub;
    }

    private FakeSourceHub RegisterFakeEnrichmentHub()
    {
        var mat = Sys.Materializer();
        var hub = new FakeSourceHub(mat);
        ActorRegistry.Register<IEnrichmentActor>(hub.Actor(Sys, typeof(RequestEnrichmentSource)), overwrite: true);
        return hub;
    }

    [Fact(Timeout = 15000)]
    public async Task Should_request_both_model_state_source_and_enrichment_source_on_startup()
    {
        var modelHub = RegisterFakeModelStateHub();
        var enrichHub = RegisterFakeEnrichmentHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await modelHub.WaitForQueue();
        await enrichHub.WaitForQueue();

        // Verify the graph is live by emitting an event and expecting output
        var forecast = CreateForecast("icon_d2");
        modelHub.Emit(new EgressEvent.PerModelUpdate("lucerne", new WeatherModel("icon_d2"), forecast));

        await transport.WaitForMessage(m => m.Topic.StartsWith("njord/")).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_per_model_update_as_mqtt_messages()
    {
        var modelHub = RegisterFakeModelStateHub();
        var enrichHub = RegisterFakeEnrichmentHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport);

        await modelHub.WaitForQueue();
        await enrichHub.WaitForQueue();

        var forecast = CreateForecast("icon_d2");
        modelHub.Emit(new EgressEvent.PerModelUpdate("lucerne", new WeatherModel("icon_d2"), forecast));

        await transport.WaitForMessage(m => m.Topic.StartsWith("njord/")).WaitAsync(TestContext.Current.CancellationToken);
        var sent = transport.Sent;
        Assert.NotEmpty(sent);
        Assert.All(sent, m => Assert.StartsWith("njord/", m.Topic));
        Assert.All(sent, m => Assert.True(m.Retain));
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_enrichment_update_through_presenter()
    {
        var modelHub = RegisterFakeModelStateHub();
        var enrichHub = RegisterFakeEnrichmentHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport, withPresenters: true);

        await modelHub.WaitForQueue();
        await enrichHub.WaitForQueue();

        var temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
        var consensus = new ConsensusResult(
        [
            new ParameterConsensus(temperature, new Dictionary<string, HorizonConsensus>
            {
                ["h3"] = new(5.0, 5.0, 0.5, 0.4, 0.9, null, null, [new WeatherModel("icon_d2")]),
            }),
        ]);
        enrichHub.Emit(new EgressEvent.EnrichmentUpdate("lucerne", "consensus", consensus));

        await transport.WaitForMessage(m => m.Topic.StartsWith("njord/lucerne/consensus")).WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public async Task Should_publish_nothing_for_an_unknown_enrichment_type_name()
    {
        var modelHub = RegisterFakeModelStateHub();
        var enrichHub = RegisterFakeEnrichmentHub();
        var transport = new RecordingTransport();

        CreateMqttStateActor(transport: transport, withPresenters: true);

        await modelHub.WaitForQueue();
        await enrichHub.WaitForQueue();

        enrichHub.Emit(new EgressEvent.EnrichmentUpdate("lucerne", "no-such-type", new object()));

        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Empty(transport.Sent);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_model_state_source_after_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestProbe = CreateTestProbe();
        ActorRegistry.Register<IModelStateActor>(Sys.ActorOf(FailingRefProvider.Props(requestProbe)), overwrite: true);
        ActorRegistry.Register<IEnrichmentActor>(CreateTestProbe().Ref, overwrite: true);

        CreateMqttStateActor();

        await requestProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
        await requestProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_enrichment_source_after_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestProbe = CreateTestProbe();
        ActorRegistry.Register<IModelStateActor>(CreateTestProbe().Ref, overwrite: true);
        ActorRegistry.Register<IEnrichmentActor>(Sys.ActorOf(FailingRefProvider.Props(requestProbe)), overwrite: true);

        CreateMqttStateActor();

        await requestProbe.ExpectMsgAsync<RequestEnrichmentSource>(cancellationToken: ct);
        await requestProbe.ExpectMsgAsync<RequestEnrichmentSource>(cancellationToken: ct);
    }

    private static ModelForecast CreateForecast(string modelId)
    {
        var temp = ParameterRegistry.GetByApiName("temperature_2m")!;
        var wind = ParameterRegistry.GetByApiName("wind_speed_10m")!;

        var points = Enumerable.Range(0, 60)
            .Select(i => new ForecastPoint(
                Anchor.AddHours(i + 1),
                new Dictionary<ParameterDef, double?>
                {
                    [temp] = 20.0 + i,
                    [wind] = 5.0 + i * 0.1,
                }))
            .ToList();

        return new ModelForecast(
            new WeatherModel(modelId), "lucerne", new CycleId(Anchor),
            new ForecastSeries(points), DailyForecastSeries.Empty);
    }

    // -- fakes ---------------------------------------------------------------

    internal sealed class FakeSourceHub
    {
        private readonly IMaterializer _mat;
        private ISourceQueueWithComplete<EgressEvent>? _queue;
        private readonly TaskCompletionSource _queueReady = new();

        public FakeSourceHub(IMaterializer mat) => _mat = mat;

        public IActorRef Actor(ActorSystem system, Type requestType)
            => system.ActorOf(Props.Create(() => new FakeSourceProvider(_mat, this, requestType)));

        public void Emit(EgressEvent evt) => _queue?.OfferAsync(evt);

        public Task WaitForQueue() => _queueReady.Task;

        internal void SetQueue(ISourceQueueWithComplete<EgressEvent> queue)
        {
            _queue = queue;
            _queueReady.TrySetResult();
        }
    }

    private sealed class FakeSourceProvider : ReceiveActor
    {
        public FakeSourceProvider(IMaterializer mat, FakeSourceHub hub, Type requestType)
        {
            if (requestType == typeof(RequestModelStateSource))
            {
                Receive<RequestModelStateSource>(msg =>
                {
                    var (queue, source) = Source.Queue<EgressEvent>(32, OverflowStrategy.DropHead)
                        .PreMaterialize(mat);
                    hub.SetQueue(queue);

                    source
                        .RunWith(StreamRefs.SourceRef<EgressEvent>(), mat)
                        .PipeTo(Sender, Self,
                            sr => new ModelStateSourceResponse(msg.RequestId, sr),
                            _ => null!);
                });
            }
            else
            {
                Receive<RequestEnrichmentSource>(msg =>
                {
                    var (queue, source) = Source.Queue<EgressEvent>(32, OverflowStrategy.DropHead)
                        .PreMaterialize(mat);
                    hub.SetQueue(queue);

                    source
                        .RunWith(StreamRefs.SourceRef<EgressEvent>(), mat)
                        .PipeTo(Sender, Self,
                            sr => new EnrichmentSourceResponse(msg.RequestId, sr),
                            _ => null!);
                });
            }
        }
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
                {
                    return Task.CompletedTask;
                }

                var tcs = new TaskCompletionSource();
                _waiters.Add((predicate, tcs));
                return tcs.Task;
            }
        }
    }
}
