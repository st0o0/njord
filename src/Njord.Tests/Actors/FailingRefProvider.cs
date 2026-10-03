using Akka.Actor;
using Njord.Egress;
using Njord.Mqtt;
using Njord.Pipeline;

namespace Njord.Tests.Actors;

/// <summary>
/// Stands in for any ref-vending actor (egress, MQTT connection, pipeline) and answers every
/// request with the matching typed failure while recording the request on a probe.
/// </summary>
internal sealed class FailingRefProvider : ReceiveActor
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
        Receive<RequestMqttSink>(msg =>
        {
            requestProbe.Tell(msg);
            Sender.Tell(new MqttSinkFailed(msg.RequestId, cause));
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
