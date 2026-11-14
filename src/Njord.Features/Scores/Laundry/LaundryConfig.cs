using Njord.Core.Configuration;

namespace Njord.Features.Scores.Laundry;

public sealed class LaundryConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Laundry";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int MinWindowScore { get; set; } = 50;
    public int MinWindowHours { get; set; } = 2;
    public int MaxWindows { get; set; } = 3;
    public double GoodHourScore { get; set; } = 60.0;
    public double RainThreshold { get; set; } = 0.1;
    public double TemperatureWeight { get; set; } = 0.25;
    public double HumidityWeight { get; set; } = 0.25;
    public double WindWeight { get; set; } = 0.15;
    public double RainWeight { get; set; } = 0.20;
    public double SunWeight { get; set; } = 0.15;
    public double TemperatureLow { get; set; } = 5.0;
    public double TemperatureHigh { get; set; } = 25.0;
    public double HumidityLow { get; set; } = 40.0;
    public double HumidityHigh { get; set; } = 90.0;
    public double WindLow { get; set; } = 0.0;
    public double WindIdeal { get; set; } = 2.0;
    public double WindModerate { get; set; } = 8.0;
    public double WindStrong { get; set; } = 15.0;
    public double SunshineFull { get; set; } = 3600.0;
}
