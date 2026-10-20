using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record WeatherPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Temperature = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ApparentTemperature = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WindSpeed = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Precipitation = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? PrecipitationProbability = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? CloudCover = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Pressure = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? WeatherCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Humidity = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DewPoint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? UvIndex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? EnsembleStdDev = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WindGusts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Visibility = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Cape = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Rain = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Snowfall = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? SoilTemperature = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? FreezingLevelHeight = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsDay = null);
