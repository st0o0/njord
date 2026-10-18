using System.Text.Json.Serialization;
using Njord.Messages.Alerts;
using Njord.Mqtt.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record WindChillAlertPayload(
    double FeltTemperature,
    double WindFactor,
    [property: JsonConverter(typeof(ExposureRiskWireConverter))] ExposureRisk? ExposureRisk) : AlertPayload;
