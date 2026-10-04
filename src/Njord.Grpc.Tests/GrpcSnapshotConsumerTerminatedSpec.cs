using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Njord.Actors;
using Njord.Domain.Weather;
using Njord.Grpc;
using Njord.Messages.Egress;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class GrpcSnapshotConsumerTerminatedSpec : Akka.Hosting.TestKit.TestKit
{
    private Akka.TestKit.TestProbe _modelStateProbe = null!;
    private Akka.TestKit.TestProbe _enrichmentProbe = null!;

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .AddTestPersistence()
            .WithActors((system, registry) =>
            {
                _modelStateProbe = CreateTestProbe();
                _enrichmentProbe = CreateTestProbe();
                var mat = system.Materializer();

                var fakeModelState = system.ActorOf(
                    Props.Create(() => new FakeModelStateActor(mat, _modelStateProbe)));
                registry.Register<IModelStateActor>(fakeModelState);

                var fakeEnrichment = system.ActorOf(
                    Props.Create(() => new FakeEnrichmentActor(mat, _enrichmentProbe)));
                registry.Register<IEnrichmentActor>(fakeEnrichment);

                registry.Register<IForecastSnapshotActor>(
                    system.ActorOf(Props.Create(() => new ForecastSnapshotActor())));
                registry.Register<IEnrichmentSnapshotActor>(
                    system.ActorOf(Props.Create(() => new EnrichmentSnapshotActor())));
            })
            .AddTestTimefactor()
            .AddFastRetryBackoff();
    }

    [Fact(Timeout = 10000)]
    public async Task Re_requests_source_after_model_state_actor_terminates()
    {
        var ct = TestContext.Current.CancellationToken;
        var consumer = Sys.ActorOf(Props.Create(() =>
            new GrpcSnapshotConsumerActor()));

        var firstRequest = await _modelStateProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
        Assert.NotNull(firstRequest);

        var oldModelState = ActorRegistry.Get<IModelStateActor>();
        Watch(oldModelState);
        await oldModelState.GracefulStop(TimeSpan.FromSeconds(2));
        await ExpectTerminatedAsync(oldModelState, cancellationToken: ct);

        var mat = Sys.Materializer();
        var newModelState = Sys.ActorOf(
            Props.Create(() => new FakeModelStateActor(mat, _modelStateProbe)));
        ActorRegistry.Register<IModelStateActor>(newModelState, overwrite: true);

        var secondRequest = await _modelStateProbe.ExpectMsgAsync<RequestModelStateSource>(TimeSpan.FromSeconds(5), cancellationToken: ct);
        Assert.NotNull(secondRequest);
    }

    [Fact(Timeout = 5000)]
    public async Task Re_requests_source_after_model_state_source_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        var failureProbe = CreateTestProbe();
        ActorRegistry.Register<IModelStateActor>(Sys.ActorOf(FailingRefProvider.Props(failureProbe)), overwrite: true);

        Sys.ActorOf(Props.Create(() => new GrpcSnapshotConsumerActor()));

        await failureProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
        await failureProbe.ExpectMsgAsync<RequestModelStateSource>(cancellationToken: ct);
    }

    private sealed class FakeModelStateActor : ReceiveActor
    {
        public FakeModelStateActor(IMaterializer mat, IActorRef requestProbe)
        {
            Receive<RequestModelStateSource>(msg =>
            {
                requestProbe.Tell(msg);
                Source.Empty<EgressEvent>()
                    .RunWith(StreamRefs.SourceRef<EgressEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new ModelStateSourceResponse(msg.RequestId, sr),
                        _ => null!);
            });
        }
    }

    private sealed class FakeEnrichmentActor : ReceiveActor
    {
        public FakeEnrichmentActor(IMaterializer mat, IActorRef requestProbe)
        {
            Receive<RequestEnrichmentSource>(msg =>
            {
                requestProbe.Tell(msg);
                Source.Empty<EgressEvent>()
                    .RunWith(StreamRefs.SourceRef<EgressEvent>(), mat)
                    .PipeTo(Sender, Self,
                        sr => new EnrichmentSourceResponse(msg.RequestId, sr),
                        _ => null!);
            });
        }
    }
}
