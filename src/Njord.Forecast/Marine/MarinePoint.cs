namespace Njord.Forecast.Marine;

public sealed record MarinePoint(
    DateTimeOffset ValidFrom,
    double WaveHeight,
    double WavePeriod,
    double WaveDirection,
    double? WaterTemperature = null) : IHasValidTime;
