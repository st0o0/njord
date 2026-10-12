using Akka.Actor;

namespace Njord.Messages.Mqtt;

public sealed record SubscribeInbound(IActorRef Listener);
