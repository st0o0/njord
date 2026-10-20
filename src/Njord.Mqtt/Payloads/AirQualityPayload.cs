using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record AirQualityPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Pm25 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Pm10 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Ozone = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? BirchPollen = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? GrassPollen = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Alder = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Mugwort = null);
