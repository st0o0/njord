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
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
