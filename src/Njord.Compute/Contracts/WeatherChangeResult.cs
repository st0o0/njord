namespace Njord.Compute.Contracts;

public sealed record WeatherChangeResult(
    WeatherCategory From,
    WeatherCategory To);
