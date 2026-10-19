using System.Text.Json.Serialization;

namespace Njord.Mqtt.Payloads;

public sealed record WeatherPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    double Temperature,
    double ApparentTemperature,
    double WindSpeed,
    double Precipitation,
    double PrecipitationProbability,
    double CloudCover,
    double Pressure,
    int WeatherCode,
    double Humidity,
    double DewPoint,
    double UvIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? EnsembleStdDev = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? WindGusts = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Visibility = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Cape = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Rain = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Snowfall = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? SoilTemperature = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? FreezingLevelHeight = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsDay = null);
