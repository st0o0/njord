using Njord.Core.Configuration;

namespace Njord.Features.Scores.Outdoor;

public sealed class OutdoorConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Outdoor";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double GoodHourScore { get; set; } = 85.0;
    public double RainThreshold { get; set; } = 0.1;
    public double TemperatureWeight { get; set; } = 0.3;
    public double RainWeight { get; set; } = 0.2;
    public double WindWeight { get; set; } = 0.1;
    public double CloudWeight { get; set; } = 0.1;
    public double UvWeight { get; set; } = 0.1;
    public double AirQualityWeight { get; set; } = 0.2;
    public double TemperatureLow { get; set; } = 0.0;
    public double TemperatureIdeal { get; set; } = 15.0;
    public double TemperatureWarm { get; set; } = 26.0;
    public double TemperatureHot { get; set; } = 36.0;
    public double WindLow { get; set; } = -1.0;
    public double WindIdeal { get; set; } = 0.0;
    public double WindModerate { get; set; } = 8.0;
    public double WindStrong { get; set; } = 16.0;
    public double CloudLow { get; set; } = 50.0;
    public double CloudHigh { get; set; } = 100.0;
    public double UvModerate { get; set; } = 5.0;
    public double UvHigh { get; set; } = 11.0;
    public double Pm25Low { get; set; } = 15.0;
    public double Pm25High { get; set; } = 55.0;
    public double PollenLow { get; set; } = 20.0;
    public double PollenHigh { get; set; } = 100.0;
}
