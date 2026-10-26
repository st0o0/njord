using Njord.Forecast.Weather;

namespace Njord.OpenMeteo.Mapping;

public static class WeatherApiVariableMappingExtensions
{
    public static WeatherApiVariable ToApiVariable(this WeatherParams param) => param switch
    {
        WeatherParams.Temperature => WeatherApiVariable.Temperature,
        WeatherParams.ApparentTemperature => WeatherApiVariable.ApparentTemperature,
        WeatherParams.WindSpeed => WeatherApiVariable.WindSpeed,
        WeatherParams.Precipitation => WeatherApiVariable.Precipitation,
        WeatherParams.PrecipitationProbability => WeatherApiVariable.PrecipitationProbability,
        WeatherParams.CloudCover => WeatherApiVariable.CloudCover,
        WeatherParams.Pressure => WeatherApiVariable.Pressure,
        WeatherParams.WeatherCode => WeatherApiVariable.WeatherCode,
        WeatherParams.Humidity => WeatherApiVariable.Humidity,
        WeatherParams.DewPoint => WeatherApiVariable.DewPoint,
        WeatherParams.UvIndex => WeatherApiVariable.UvIndex,
        WeatherParams.EnsembleStdDev => WeatherApiVariable.EnsembleStdDev,
        WeatherParams.WindGusts => WeatherApiVariable.WindGusts,
        WeatherParams.Visibility => WeatherApiVariable.Visibility,
        WeatherParams.Cape => WeatherApiVariable.Cape,
        WeatherParams.Rain => WeatherApiVariable.Rain,
        WeatherParams.Snowfall => WeatherApiVariable.Snowfall,
        WeatherParams.SoilTemperature => WeatherApiVariable.SoilTemperature,
        WeatherParams.FreezingLevelHeight => WeatherApiVariable.FreezingLevelHeight,
        WeatherParams.IsDay => WeatherApiVariable.IsDay,
        WeatherParams.ShortwaveRadiation => WeatherApiVariable.ShortwaveRadiation,
        WeatherParams.DirectRadiation => WeatherApiVariable.DirectRadiation,
        WeatherParams.DiffuseRadiation => WeatherApiVariable.DiffuseRadiation,
        WeatherParams.DirectNormalIrradiance => WeatherApiVariable.DirectNormalIrradiance,
        WeatherParams.SunshineDuration => WeatherApiVariable.SunshineDuration,
        WeatherParams.WindDirection => WeatherApiVariable.WindDirection,
        WeatherParams.GlobalTiltedIrradiance => WeatherApiVariable.GlobalTiltedIrradiance,
        WeatherParams.SnowDepth => WeatherApiVariable.SnowDepth,
        WeatherParams.WetBulbTemperature => WeatherApiVariable.WetBulbTemperature,
        WeatherParams.SnowfallHeight => WeatherApiVariable.SnowfallHeight,
        WeatherParams.PrecipitationSpread => WeatherApiVariable.PrecipitationSpread,
        WeatherParams.WindSpeedSpread => WeatherApiVariable.WindSpeedSpread,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<WeatherApiVariable> ToApiVariables(this WeatherParams flags) =>
        [.. Enum.GetValues<WeatherParams>().Where(p => p != WeatherParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this WeatherParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
