using Akka.Actor;

namespace Njord.Egress;

public sealed record RegisterPublisher(IActorRef Publisher);
public sealed record UnregisterPublisher(IActorRef Publisher);
public sealed record PublishStateResult(string Location, object Result);

public sealed class EgressActor : ReceiveActor
{
    private readonly HashSet<IActorRef> _publishers = [];

    public EgressActor()
    {
        Receive<RegisterPublisher>(msg =>
        {
            if (_publishers.Add(msg.Publisher))
                Context.Watch(msg.Publisher);
        });

        Receive<UnregisterPublisher>(msg =>
        {
            if (_publishers.Remove(msg.Publisher))
                Context.Unwatch(msg.Publisher);
        });

        Receive<PublishStateResult>(msg =>
        {
            foreach (var publisher in _publishers)
                publisher.Tell(msg);
        });

        Receive<Terminated>(msg => _publishers.Remove(msg.ActorRef));
    }
}
