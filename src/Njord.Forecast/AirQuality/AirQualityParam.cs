namespace Njord.Forecast.AirQuality;

[Flags]
public enum AirQualityParam
{
    None = 0,
    Pm25 = 1 << 0,
    Pm10 = 1 << 1,
    Ozone = 1 << 2,
    NitrogenDioxide = 1 << 3,
    EuropeanAqi = 1 << 4,
    BirchPollen = 1 << 5,
    GrassPollen = 1 << 6,
    Alder = 1 << 7,
    Mugwort = 1 << 8,
}
