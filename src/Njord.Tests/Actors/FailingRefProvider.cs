using Akka.Actor;
using Njord.Actors;
using Njord.Mqtt;

namespace Njord.Tests.Actors;

internal sealed class FailingRefProvider : ReceiveActor
{
    public static Props Props(IActorRef requestProbe) =>
        Akka.Actor.Props.Create(() => new FailingRefProvider(requestProbe));

    public FailingRefProvider(IActorRef requestProbe)
    {
        var cause = new InvalidOperationException("simulated stream ref failure");

        Receive<RequestModelStateSource>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new ModelStateSourceFailed(msg.RequestId, cause));
        });
        Receive<RequestEnrichmentSource>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new EnrichmentSourceFailed(msg.RequestId, cause));
        });
        Receive<RequestPipelineSink>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new PipelineSinkFailed(msg.RequestId, cause));
        });
        Receive<RequestPipelineSource>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new PipelineSourceFailed(msg.RequestId, cause));
        });
        Receive<SubscribeInbound>(_ => { });
    }
}
