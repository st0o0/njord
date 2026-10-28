namespace Njord.Messages.Pipeline;

public enum LimitingKind
{
    None,
    Rain,
    Uv,
    Wind,
    TemperatureHigh,
    TemperatureLow,
    Humidity,
    Pollen,
    AirQuality,
    Visibility,
    Thunderstorm,
    Darkness
}

public sealed record LimitingFactor(LimitingKind Kind, DateTimeOffset From, double Value);

public sealed record ActivityWindow(DateTimeOffset Start, DateTimeOffset End, int Score, LimitingFactor? Limiting);
