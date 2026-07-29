using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Domain.Weather;
using Njord.Egress;
using Njord.Grpc;
using Njord.Tests.Shared;

namespace Njord.Tests.Grpc;

public sealed class GrpcSnapshotConsumerTerminatedSpec : Akka.Hosting.TestKit.TestKit
{
    private Akka.TestKit.TestProbe _requestProbe = null!;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .WithActors((system, registry) =>
            {
                _requestProbe = CreateTestProbe();
                var mat = system.Materializer();

                var fakeEgress = system.ActorOf(
                    Props.Create(() => new FakeEgressActor(mat, _requestProbe)));
                registry.Register<EgressActor>(fakeEgress);

                registry.Register<ForecastSnapshotActor>(
                    system.ActorOf(Props.Create(() => new ForecastSnapshotActor())));
                registry.Register<EnrichmentSnapshotActor>(
                    system.ActorOf(Props.Create(() => new EnrichmentSnapshotActor())));
            });
    }

    [Fact(Timeout = 10000)]
    public async Task Re_requests_source_after_egress_actor_terminates()
    {
        var consumer = Sys.ActorOf(Props.Create(() =>
            new GrpcSnapshotConsumerActor(NullLogger<GrpcSnapshotConsumerActor>.Instance)));

        var firstRequest = await _requestProbe.ExpectMsgAsync<RequestEgressSource>();
        Assert.NotNull(firstRequest);

        var oldEgress = ActorRegistry.Get<EgressActor>();
        await oldEgress.GracefulStop(TimeSpan.FromSeconds(2));

        await Task.Delay(200);

        var mat = Sys.Materializer();
        var newEgress = Sys.ActorOf(
            Props.Create(() => new FakeEgressActor(mat, _requestProbe)));
        ActorRegistry.Register<EgressActor>(newEgress, overwrite: true);

        var secondRequest = await _requestProbe.ExpectMsgAsync<RequestEgressSource>(TimeSpan.FromSeconds(5));
        Assert.NotNull(secondRequest);
    }

    private sealed class FakeEgressActor : ReceiveActor
    {
        public FakeEgressActor(IMaterializer mat, IActorRef requestProbe)
        {
            Receive<RequestEgressSource>(msg =>
            {
                requestProbe.Tell(msg);
                Source.Empty<EgressEvent>()
                    .RunWith(StreamRefs.SourceRef<EgressEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new EgressSourceResponse(sr),
                        _ => null!);
            });
        }
    }
}
