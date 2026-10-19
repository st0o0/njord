using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record AirQualityPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    double Pm25,
    double Pm10,
    double Ozone,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? BirchPollen = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? GrassPollen = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Alder = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Mugwort = null);
