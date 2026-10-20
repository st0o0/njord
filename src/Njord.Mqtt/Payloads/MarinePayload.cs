using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record MarinePayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WaveHeight = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WavePeriod = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WaveDirection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WaterTemperature = null);
