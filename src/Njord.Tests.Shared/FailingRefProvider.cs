using Akka.Actor;
using Njord.Messages.Egress;
using Njord.Messages.Pipeline;

namespace Njord.Tests.Shared;

/// <summary>
/// Stands in for any ref-vending actor (egress, pipeline) and answers every
/// request with the matching typed failure while recording the request on a probe.
/// </summary>
public sealed class FailingRefProvider : ReceiveActor
{
    public static Props Props(IActorRef requestProbe) =>
        Akka.Actor.Props.Create(() => new FailingRefProvider(requestProbe));

    public FailingRefProvider(IActorRef requestProbe)
    {
        var cause = new InvalidOperationException("simulated stream ref failure");

        Receive<RequestEgressSink>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new EgressSinkFailed(msg.RequestId, cause));
        });
        Receive<RequestEgressSource>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new EgressSourceFailed(msg.RequestId, cause));
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
    }
}
