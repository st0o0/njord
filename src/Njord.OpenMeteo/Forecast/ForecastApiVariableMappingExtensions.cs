using Njord.Core.Forecast;

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
        ForecastParams.SoilMoisture0To1Cm => ForecastApiVariable.SoilMoisture0To1Cm,
        ForecastParams.SoilMoisture1To3Cm => ForecastApiVariable.SoilMoisture1To3Cm,
        ForecastParams.Evapotranspiration => ForecastApiVariable.Evapotranspiration,
        ForecastParams.VapourPressureDeficit => ForecastApiVariable.VapourPressureDeficit,
        ForecastParams.BoundaryLayerHeight => ForecastApiVariable.BoundaryLayerHeight,
        ForecastParams.CloudCoverLow => ForecastApiVariable.CloudCoverLow,
        ForecastParams.CloudCoverMid => ForecastApiVariable.CloudCoverMid,
        ForecastParams.CloudCoverHigh => ForecastApiVariable.CloudCoverHigh,
        ForecastParams.ConvectiveInhibition => ForecastApiVariable.ConvectiveInhibition,
        ForecastParams.LiftedIndex => ForecastApiVariable.LiftedIndex,
        ForecastParams.SurfacePressure => ForecastApiVariable.SurfacePressure,
        ForecastParams.WindSpeed80M => ForecastApiVariable.WindSpeed80M,
        ForecastParams.WindDirection80M => ForecastApiVariable.WindDirection80M,
        ForecastParams.WindSpeed120M => ForecastApiVariable.WindSpeed120M,
        ForecastParams.WindDirection120M => ForecastApiVariable.WindDirection120M,
        ForecastParams.Temperature80M => ForecastApiVariable.Temperature80M,
        ForecastParams.Temperature120M => ForecastApiVariable.Temperature120M,
        ForecastParams.WindSpeed100M => ForecastApiVariable.WindSpeed100M,
        ForecastParams.WindDirection100M => ForecastApiVariable.WindDirection100M,
        ForecastParams.ThunderstormProbability => ForecastApiVariable.ThunderstormProbability,
        ForecastParams.PrecipitationType => ForecastApiVariable.PrecipitationType,
        ForecastParams.SurfaceTemperature => ForecastApiVariable.SurfaceTemperature,
        ForecastParams.SnowfallWaterEquivalent => ForecastApiVariable.SnowfallWaterEquivalent,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<ForecastApiVariable> ToApiVariables(this ForecastParams flags) =>
        [.. Enum.GetValues<ForecastParams>().Where(p => p != ForecastParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this ForecastParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
