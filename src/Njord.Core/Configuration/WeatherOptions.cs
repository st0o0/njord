namespace Njord.Core.Configuration;

public sealed class WeatherOptions
{
    public const string SectionName = "Njord:Weather";

    public bool IncludeSolar { get; set; }

    public int ForecastDays { get; set; } = 4;
}
