using Njord.Forecast.Forecast;

namespace Njord.OpenMeteo.Forecast;

public static class ForecastApiVariableMappingExtensions
{
    public static ForecastApiVariable ToApiVariable(this ForecastParams param) => param switch
    {
        ForecastParams.Temperature => ForecastApiVariable.Temperature,
        ForecastParams.ApparentTemperature => ForecastApiVariable.ApparentTemperature,
        ForecastParams.WindSpeed => ForecastApiVariable.WindSpeed,
        ForecastParams.Precipitation => ForecastApiVariable.Precipitation,
        ForecastParams.PrecipitationProbability => ForecastApiVariable.PrecipitationProbability,
        ForecastParams.CloudCover => ForecastApiVariable.CloudCover,
        ForecastParams.Pressure => ForecastApiVariable.Pressure,
        ForecastParams.WeatherCode => ForecastApiVariable.WeatherCode,
        ForecastParams.Humidity => ForecastApiVariable.Humidity,
        ForecastParams.DewPoint => ForecastApiVariable.DewPoint,
        ForecastParams.UvIndex => ForecastApiVariable.UvIndex,
        ForecastParams.WindGusts => ForecastApiVariable.WindGusts,
        ForecastParams.Visibility => ForecastApiVariable.Visibility,
        ForecastParams.Cape => ForecastApiVariable.Cape,
        ForecastParams.Rain => ForecastApiVariable.Rain,
        ForecastParams.Snowfall => ForecastApiVariable.Snowfall,
        ForecastParams.SoilTemperature => ForecastApiVariable.SoilTemperature,
        ForecastParams.FreezingLevelHeight => ForecastApiVariable.FreezingLevelHeight,
        ForecastParams.IsDay => ForecastApiVariable.IsDay,
        ForecastParams.ShortwaveRadiation => ForecastApiVariable.ShortwaveRadiation,
        ForecastParams.DirectRadiation => ForecastApiVariable.DirectRadiation,
        ForecastParams.DiffuseRadiation => ForecastApiVariable.DiffuseRadiation,
        ForecastParams.DirectNormalIrradiance => ForecastApiVariable.DirectNormalIrradiance,
        ForecastParams.SunshineDuration => ForecastApiVariable.SunshineDuration,
        ForecastParams.WindDirection => ForecastApiVariable.WindDirection,
        ForecastParams.GlobalTiltedIrradiance => ForecastApiVariable.GlobalTiltedIrradiance,
        ForecastParams.SnowDepth => ForecastApiVariable.SnowDepth,
        ForecastParams.WetBulbTemperature => ForecastApiVariable.WetBulbTemperature,
        ForecastParams.SnowfallHeight => ForecastApiVariable.SnowfallHeight,
        ForecastParams.UvIndexClearSky => ForecastApiVariable.UvIndexClearSky,
        ForecastParams.Showers => ForecastApiVariable.Showers,
        ForecastParams.SoilMoisture0To1cm => ForecastApiVariable.SoilMoisture0To1cm,
        ForecastParams.SoilMoisture1To3cm => ForecastApiVariable.SoilMoisture1To3cm,
        ForecastParams.Evapotranspiration => ForecastApiVariable.Evapotranspiration,
        ForecastParams.VapourPressureDeficit => ForecastApiVariable.VapourPressureDeficit,
        ForecastParams.BoundaryLayerHeight => ForecastApiVariable.BoundaryLayerHeight,
        ForecastParams.CloudCoverLow => ForecastApiVariable.CloudCoverLow,
        ForecastParams.CloudCoverMid => ForecastApiVariable.CloudCoverMid,
        ForecastParams.CloudCoverHigh => ForecastApiVariable.CloudCoverHigh,
        ForecastParams.ConvectiveInhibition => ForecastApiVariable.ConvectiveInhibition,
        ForecastParams.LiftedIndex => ForecastApiVariable.LiftedIndex,
        ForecastParams.SurfacePressure => ForecastApiVariable.SurfacePressure,
        ForecastParams.WindSpeed80m => ForecastApiVariable.WindSpeed80m,
        ForecastParams.WindDirection80m => ForecastApiVariable.WindDirection80m,
        ForecastParams.WindSpeed120m => ForecastApiVariable.WindSpeed120m,
        ForecastParams.WindDirection120m => ForecastApiVariable.WindDirection120m,
        ForecastParams.Temperature80m => ForecastApiVariable.Temperature80m,
        ForecastParams.Temperature120m => ForecastApiVariable.Temperature120m,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<ForecastApiVariable> ToApiVariables(this ForecastParams flags) =>
        [.. Enum.GetValues<ForecastParams>().Where(p => p != ForecastParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this ForecastParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
