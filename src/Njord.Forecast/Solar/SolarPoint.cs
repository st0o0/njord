namespace Njord.Forecast.Solar;

public sealed record SolarPoint(
    DateTimeOffset ValidFrom,
    double DirectRadiation,
    double DiffuseRadiation,
    double ShortwaveRadiation,
    double SunshineDuration) : IHasValidTime;
