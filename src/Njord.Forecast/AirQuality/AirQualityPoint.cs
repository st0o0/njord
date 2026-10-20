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
    double? NitrogenDioxide = null) : IHasValidTime
{
    private static readonly AirQualityParam[] Flags =
        [.. Enum.GetValues<AirQualityParam>().Where(p => p != AirQualityParam.None)];

    public bool HasFields(AirQualityParam required) =>
        Flags.All(flag => !required.HasFlag(flag) || Has(flag));

    private bool Has(AirQualityParam param) => param switch
    {
        AirQualityParam.Pm25 => Pm25 is not null,
        AirQualityParam.Pm10 => Pm10 is not null,
        AirQualityParam.Ozone => Ozone is not null,
        AirQualityParam.NitrogenDioxide => NitrogenDioxide is not null,
        AirQualityParam.EuropeanAqi => EuropeanAqi is not null,
        AirQualityParam.BirchPollen => BirchPollen is not null,
        AirQualityParam.GrassPollen => GrassPollen is not null,
        AirQualityParam.Alder => Alder is not null,
        AirQualityParam.Mugwort => Mugwort is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
