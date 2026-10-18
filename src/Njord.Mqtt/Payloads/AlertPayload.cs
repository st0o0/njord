using System.Text.Json.Serialization;
using Njord.Messages.Alerts;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

[JsonDerivedType(typeof(NoDetailsAlertPayload))]
[JsonDerivedType(typeof(FrostAlertPayload))]
[JsonDerivedType(typeof(HeatAlertPayload))]
[JsonDerivedType(typeof(StormAlertPayload))]
[JsonDerivedType(typeof(HeavyRainAlertPayload))]
[JsonDerivedType(typeof(UvAlertPayload))]
[JsonDerivedType(typeof(FogAlertPayload))]
[JsonDerivedType(typeof(SnowAlertPayload))]
[JsonDerivedType(typeof(PressureDropAlertPayload))]
[JsonDerivedType(typeof(ThunderstormAlertPayload))]
[JsonDerivedType(typeof(IceAlertPayload))]
[JsonDerivedType(typeof(WindChillAlertPayload))]
[JsonDerivedType(typeof(VisibilityAlertPayload))]
[JsonDerivedType(typeof(TropicalNightAlertPayload))]
[JsonDerivedType(typeof(HumidityAlertPayload))]
public abstract record AlertPayload
{
    [JsonPropertyOrder(-7)]
    [JsonConverter(typeof(AlertSeverityWireConverter))]
    public AlertSeverity Severity { get; init; }

    [JsonPropertyOrder(-6)]
    public double Confidence { get; init; }

    [JsonPropertyOrder(-5)]
    public double TriggerValue { get; init; }

    [JsonPropertyOrder(-4)]
    public double Threshold { get; init; }

    [JsonPropertyOrder(-3)]
    public double? PeakValue { get; init; }

    [JsonPropertyOrder(-2)]
    public int? HoursUntil { get; init; }

    [JsonPropertyOrder(-1)]
    public int? DurationHours { get; init; }
}
