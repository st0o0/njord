namespace Njord.Model;

public sealed record MarinePoint(
    DateTimeOffset ValidFrom,
    double WaveHeight,
    double WavePeriod,
    double WaveDirection,
    double? WaterTemperature = null) : IHasValidTime;
