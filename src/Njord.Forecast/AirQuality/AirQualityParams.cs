namespace Njord.Forecast.AirQuality;

[Flags]
public enum AirQualityParams
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
    CarbonMonoxide = 1 << 9,
    SulphurDioxide = 1 << 10,
    Dust = 1 << 11,
    Ammonia = 1 << 12,
    OlivePollen = 1 << 13,
    UsAqi = 1 << 14,
    UvIndex = 1 << 15,
    UvIndexClearSky = 1 << 16,
    RagweedPollen = 1 << 17,
    EuropeanAqiPm25 = 1 << 18,
    EuropeanAqiPm10 = 1 << 19,
    EuropeanAqiNitrogenDioxide = 1 << 20,
    EuropeanAqiOzone = 1 << 21,
    EuropeanAqiSulphurDioxide = 1 << 22,
    AerosolOpticalDepth = 1 << 23,
}
