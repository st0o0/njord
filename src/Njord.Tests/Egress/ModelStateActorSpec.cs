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
using Njord.Pipeline;
using Njord.Tests.Shared;

namespace Njord.Tests.Egress;

public sealed class ModelStateActorSpec : IDisposable
{
    private readonly ActorSystem _system = ActorSystem.Create("model-state-spec");

    public void Dispose() => _system.Dispose();

    private static NjordOptions DefaultOptions() => new()
    {
        Locations = [new LocationOptions { Name = "lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    private IActorRef CreateModelStateActor()
    {
        var options = DefaultOptions();
        var parameters = ParameterRegistry.Resolve(["Weather"], [], []);

        return _system.ActorOf(Props.Create(() => new ModelStateActor(
            Microsoft.Extensions.Options.Options.Create(options),
            parameters,
            TimeProvider.System,
            NullLogger<ModelStateActor>.Instance)));
    }

    [Fact(Timeout = 5000)]
    public async Task Requests_egress_sink_and_pipeline_source_on_startup()
    {
        var registry = ActorRegistry.For(_system);
        var mat = _system.Materializer();
        var egressRequests = new ConcurrentBag<RequestEgressSink>();
        var pipelineRequests = new ConcurrentBag<RequestPipelineSource>();

        var fakeEgress = _system.ActorOf(FakeEgressSinkProvider.Props(mat, egressRequests));
        var fakePipeline = _system.ActorOf(FakePipelineSource.Props(mat, pipelineRequests));

        registry.Register<EgressActor>(fakeEgress, overwrite: true);
        registry.Register<PipelineActor>(fakePipeline, overwrite: true);

        CreateModelStateActor();

        await AsyncAssert.WaitUntil(() => egressRequests.Count >= 1 && pipelineRequests.Count >= 1);

        Assert.Single(egressRequests);
        Assert.Single(pipelineRequests);
    }

    private sealed class FakeEgressSinkProvider : ReceiveActor
    {
        public FakeEgressSinkProvider(IMaterializer mat, ConcurrentBag<RequestEgressSink>? requests = null)
        {
            Receive<RequestEgressSink>(msg =>
            {
                requests?.Add(msg);
                var sinkRef = StreamRefs.SinkRef<EgressEvent>()
                    .To(Sink.Ignore<EgressEvent>().MapMaterializedValue(_ => Akka.NotUsed.Instance))
                    .Run(mat);
                sinkRef.PipeTo(Sender, Self,
                    sr => new EgressSinkResponse(sr),
                    _ => null!);
            });
        }

        public static Props Props(IMaterializer mat, ConcurrentBag<RequestEgressSink>? requests = null) =>
            Akka.Actor.Props.Create(() => new FakeEgressSinkProvider(mat, requests));
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

        public static Props Props(IMaterializer mat, ConcurrentBag<RequestPipelineSource>? requests = null) =>
            Akka.Actor.Props.Create(() => new FakePipelineSource(mat, requests));
    }
}
