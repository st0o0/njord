namespace Njord.Domain;

/// <summary>The closed v1 parameter set. Extending it is a spec change, not a code tweak.</summary>
public enum WeatherParameter
{
    Temperature,
    Precipitation,
    WindSpeed,
    WindGust,
    Dewpoint,
    RelativeHumidity,
    CloudCover,
    PressureMsl,
}

public static class WeatherParameterExtensions
{
    public static string Unit(this WeatherParameter parameter) => parameter switch
    {
        WeatherParameter.Temperature or WeatherParameter.Dewpoint => "°C",
        WeatherParameter.Precipitation => "mm",
        WeatherParameter.WindSpeed or WeatherParameter.WindGust => "m/s",
        WeatherParameter.RelativeHumidity or WeatherParameter.CloudCover => "%",
        WeatherParameter.PressureMsl => "hPa",
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
    };
}
