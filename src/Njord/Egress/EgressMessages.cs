using Njord.Domain;

namespace Njord.Egress;

/// <summary>
/// Telemetry hand-off from the pipeline to the egress: only domain forecasts
/// cross the boundary (ingest and egress never meet).
/// </summary>
public sealed record PublishTelemetry(IReadOnlyList<ModelForecast> Forecasts);

/// <summary>Timing knobs, overridable in tests.</summary>
public sealed record MqttEgressTuning(TimeSpan ReconnectDelay)
{
    public static MqttEgressTuning Default { get; } = new(TimeSpan.FromSeconds(5));
}
