using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record FloodPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    double Discharge,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeP10 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeP90 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MedianHistorical = null);
