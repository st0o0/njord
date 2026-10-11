using Akka.Actor;

namespace Njord.Messages.Mqtt;

public sealed record MqttConnected;

public sealed record MqttInboundMessage(string Topic, string Payload);

public sealed record SubscribeInbound(IActorRef Listener);
