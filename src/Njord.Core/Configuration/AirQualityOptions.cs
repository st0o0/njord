namespace Njord.Core.Configuration;

public sealed class AirQualityOptions
{
    public const string SectionName = "Njord:AirQuality";

    public int ForecastDays { get; set; } = 5;
}
