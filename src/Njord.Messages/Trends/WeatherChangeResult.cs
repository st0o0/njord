namespace Njord.Messages.Trends;

public sealed record WeatherChangeResult(
    WeatherCategory From,
    WeatherCategory To);
