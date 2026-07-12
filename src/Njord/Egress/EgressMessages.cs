using Akka.Streams;

namespace Njord.Egress;

/// <summary>Request the egress actor's SinkRef for publishing MqttMessages.</summary>
public sealed record RequestEgressSink;

/// <summary>Response carrying the materialized SinkRef into the egress MergeHub.</summary>
public sealed record EgressSinkResponse(ISinkRef<MqttMessage> SinkRef);

/// <summary>Timing knobs, overridable in tests.</summary>
public sealed record MqttEgressTuning(TimeSpan ReconnectDelay)
{
    public static MqttEgressTuning Default { get; } = new(TimeSpan.FromSeconds(5));
}
