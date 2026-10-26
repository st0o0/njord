using Njord.Forecast.AirQuality;

namespace Njord.OpenMeteo.AirQuality;

public static class AirQualityApiVariableMappingExtensions
{
    public static AirQualityApiVariable ToApiVariable(this AirQualityParams param) => param switch
    {
        AirQualityParams.Pm25 => AirQualityApiVariable.Pm25,
        AirQualityParams.Pm10 => AirQualityApiVariable.Pm10,
        AirQualityParams.Ozone => AirQualityApiVariable.Ozone,
        AirQualityParams.NitrogenDioxide => AirQualityApiVariable.NitrogenDioxide,
        AirQualityParams.EuropeanAqi => AirQualityApiVariable.EuropeanAqi,
        AirQualityParams.BirchPollen => AirQualityApiVariable.BirchPollen,
        AirQualityParams.GrassPollen => AirQualityApiVariable.GrassPollen,
        AirQualityParams.Alder => AirQualityApiVariable.Alder,
        AirQualityParams.Mugwort => AirQualityApiVariable.Mugwort,
        AirQualityParams.CarbonMonoxide => AirQualityApiVariable.CarbonMonoxide,
        AirQualityParams.SulphurDioxide => AirQualityApiVariable.SulphurDioxide,
        AirQualityParams.Dust => AirQualityApiVariable.Dust,
        AirQualityParams.Ammonia => AirQualityApiVariable.Ammonia,
        AirQualityParams.OlivePollen => AirQualityApiVariable.OlivePollen,
        AirQualityParams.UsAqi => AirQualityApiVariable.UsAqi,
        AirQualityParams.UvIndex => AirQualityApiVariable.UvIndex,
        AirQualityParams.UvIndexClearSky => AirQualityApiVariable.UvIndexClearSky,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<AirQualityApiVariable> ToApiVariables(this AirQualityParams flags) =>
        [.. Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this AirQualityParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
