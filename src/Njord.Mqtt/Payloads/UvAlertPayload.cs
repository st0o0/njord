using System.Text.Json.Serialization;
using Njord.Messages.Alerts;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record UvAlertPayload(
    [property: JsonConverter(typeof(UvLevelWireConverter))] UvLevel UvLevel,
    double UvIndex) : AlertPayload;
