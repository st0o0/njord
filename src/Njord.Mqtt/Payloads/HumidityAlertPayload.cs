using System.Text.Json.Serialization;
using Njord.Messages.Alerts;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record HumidityAlertPayload(
    double MaxDewpoint,
    [property: JsonConverter(typeof(HumidityComfortWireConverter))] HumidityComfort? ComfortLevel) : AlertPayload;
