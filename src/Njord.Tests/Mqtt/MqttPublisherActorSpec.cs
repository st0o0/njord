using System.Collections.Concurrent;
using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Ingest;
using Njord.Mqtt;
using Njord.Pipeline;

namespace Njord.Tests.Mqtt;

public sealed class MqttPublisherActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("mqtt-publisher-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreatePublisherActor()
    {
        var options = DefaultOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return _system.ActorOf(Props.Create(() => new MqttPublisherActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            TimeProvider.System,
            NullLogger<MqttPublisherActor>.Instance)));
    }

    [Fact(Timeout = 5000)]
    public async Task Registers_with_egress_actor_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var inbox = new ConcurrentBag<RegisterPublisher>();

        var fakeEgress = _system.ActorOf(FakeEgressActor.Props(inbox));
        var fakeConnection = _system.ActorOf(FakeMqttSinkProvider.Props(mat));
        var fakePipeline = _system.ActorOf(FakePipelineSource.Props(mat));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<MqttConnectionActor>(fakeConnection, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreatePublisherActor();

        await Task.Delay(500);

        Assert.Single(inbox);
        Assert.NotNull(inbox.First().Publisher);
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_sink_ref_from_mqtt_connection_actor_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var sinkRequests = new ConcurrentBag<RequestMqttSink>();

        var fakeEgress = _system.ActorOf(FakeEgressActor.Props(new ConcurrentBag<RegisterPublisher>()));
        var fakeConnection = _system.ActorOf(FakeMqttSinkProvider.Props(mat, sinkRequests));
        var fakePipeline = _system.ActorOf(FakePipelineSource.Props(mat));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<MqttConnectionActor>(fakeConnection, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreatePublisherActor();

        await Task.Delay(500);

        Assert.Single(sinkRequests);
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_source_ref_from_pipeline_actor_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var sourceRequests = new ConcurrentBag<RequestPipelineSource>();

        var fakeEgress = _system.ActorOf(FakeEgressActor.Props(new ConcurrentBag<RegisterPublisher>()));
        var fakeConnection = _system.ActorOf(FakeMqttSinkProvider.Props(mat));
        var fakePipeline = _system.ActorOf(FakePipelineSource.Props(mat, sourceRequests));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<MqttConnectionActor>(fakeConnection, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreatePublisherActor();

        await Task.Delay(500);

        Assert.Single(sourceRequests);
    }

    private sealed class FakeEgressActor : ReceiveActor
    {
        private readonly ConcurrentBag<RegisterPublisher> _received;

        public FakeEgressActor(ConcurrentBag<RegisterPublisher> received)
        {
            _received = received;
            Receive<RegisterPublisher>(msg => _received.Add(msg));
        }

        public static Akka.Actor.Props Props(ConcurrentBag<RegisterPublisher> received) =>
            Akka.Actor.Props.Create(() => new FakeEgressActor(received));
    }

    private sealed class FakeMqttSinkProvider : ReceiveActor
    {
        public FakeMqttSinkProvider(IMaterializer mat, ConcurrentBag<RequestMqttSink>? requests = null)
        {
            Receive<RequestMqttSink>(msg =>
            {
                requests?.Add(msg);
                var sinkRef = StreamRefs.SinkRef<MqttMessage>()
                    .To(Sink.Ignore<MqttMessage>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new MqttSinkResponse(sr),
                    _ => null!);
            });
        }

        public static Akka.Actor.Props Props(IMaterializer mat, ConcurrentBag<RequestMqttSink>? requests = null) =>
            Akka.Actor.Props.Create(() => new FakeMqttSinkProvider(mat, requests));
    }

    private sealed class FakePipelineSource : ReceiveActor
    {
        public FakePipelineSource(IMaterializer mat, ConcurrentBag<RequestPipelineSource>? requests = null)
        {
            Receive<RequestPipelineSource>(msg =>
            {
                requests?.Add(msg);
                var sourceRef = Source.Empty<FetchOutcome>()
                    .RunWith(StreamRefs.SourceRef<FetchOutcome>(), mat)
                    .Result;
                Sender.Tell(new PipelineSourceResponse(sourceRef));
            });
        }

        public static Akka.Actor.Props Props(IMaterializer mat, ConcurrentBag<RequestPipelineSource>? requests = null) =>
            Akka.Actor.Props.Create(() => new FakePipelineSource(mat, requests));
    }
}
