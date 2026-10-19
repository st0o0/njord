namespace Njord.Mqtt.Payloads;

public sealed record SolarPayload(
    DateTimeOffset ValidFrom,
    DateTimeOffset ModelRun,
    double DirectRadiation,
    double DiffuseRadiation,
    double ShortwaveRadiation,
    double SunshineDuration);
