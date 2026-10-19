using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record MarinePayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    double WaveHeight,
    double WavePeriod,
    double WaveDirection,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WaterTemperature = null);
