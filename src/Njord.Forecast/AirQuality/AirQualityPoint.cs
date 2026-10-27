namespace Njord.Forecast.AirQuality;

public sealed record AirQualityPoint(
    DateTimeOffset ValidFrom,
    double? Pm25 = null,
    double? Pm10 = null,
    double? Ozone = null,
    double? BirchPollen = null,
    double? GrassPollen = null,
    double? Alder = null,
    double? Mugwort = null,
    double? EuropeanAqi = null,
    double? NitrogenDioxide = null,
    double? CarbonMonoxide = null,
    double? SulphurDioxide = null,
    double? Dust = null,
    double? Ammonia = null,
    double? OlivePollen = null,
    double? UsAqi = null,
    double? UvIndex = null,
    double? UvIndexClearSky = null,
    double? RagweedPollen = null,
    double? EuropeanAqiPm25 = null,
    double? EuropeanAqiPm10 = null,
    double? EuropeanAqiNitrogenDioxide = null,
    double? EuropeanAqiOzone = null,
    double? EuropeanAqiSulphurDioxide = null,
    double? AerosolOpticalDepth = null) : IHasValidTime
{
    private static readonly AirQualityParams[] Flags =
        [.. Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None)];

    public bool HasFields(AirQualityParams required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(AirQualityParams param) => param switch
    {
        AirQualityParams.Pm25 => Pm25 is not null,
        AirQualityParams.Pm10 => Pm10 is not null,
        AirQualityParams.Ozone => Ozone is not null,
        AirQualityParams.NitrogenDioxide => NitrogenDioxide is not null,
        AirQualityParams.EuropeanAqi => EuropeanAqi is not null,
        AirQualityParams.BirchPollen => BirchPollen is not null,
        AirQualityParams.GrassPollen => GrassPollen is not null,
        AirQualityParams.Alder => Alder is not null,
        AirQualityParams.Mugwort => Mugwort is not null,
        AirQualityParams.CarbonMonoxide => CarbonMonoxide is not null,
        AirQualityParams.SulphurDioxide => SulphurDioxide is not null,
        AirQualityParams.Dust => Dust is not null,
        AirQualityParams.Ammonia => Ammonia is not null,
        AirQualityParams.OlivePollen => OlivePollen is not null,
        AirQualityParams.UsAqi => UsAqi is not null,
        AirQualityParams.UvIndex => UvIndex is not null,
        AirQualityParams.UvIndexClearSky => UvIndexClearSky is not null,
        AirQualityParams.RagweedPollen => RagweedPollen is not null,
        AirQualityParams.EuropeanAqiPm25 => EuropeanAqiPm25 is not null,
        AirQualityParams.EuropeanAqiPm10 => EuropeanAqiPm10 is not null,
        AirQualityParams.EuropeanAqiNitrogenDioxide => EuropeanAqiNitrogenDioxide is not null,
        AirQualityParams.EuropeanAqiOzone => EuropeanAqiOzone is not null,
        AirQualityParams.EuropeanAqiSulphurDioxide => EuropeanAqiSulphurDioxide is not null,
        AirQualityParams.AerosolOpticalDepth => AerosolOpticalDepth is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
