using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record FloodPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Discharge = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeMean = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeMedian = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeMax = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeMin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeP25 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DischargeP75 = null);
