namespace Njord.Model;

public sealed record AirQualityPoint(
    DateTimeOffset ValidFrom,
    double Pm25,
    double Pm10,
    double Ozone,
    double? BirchPollen = null,
    double? GrassPollen = null,
    double? Alder = null,
    double? Mugwort = null) : IHasValidTime;
