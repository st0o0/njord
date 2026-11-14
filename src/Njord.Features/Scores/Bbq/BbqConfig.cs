using Njord.Core.Configuration;

namespace Njord.Features.Scores.Bbq;

public sealed class BbqConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Bbq";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double GoodHourScore { get; set; } = 70.0;
    public double RainThreshold { get; set; } = 0.1;
    public double TemperatureWeight { get; set; } = 0.35;
    public double WindWeight { get; set; } = 0.20;
    public double RainWeight { get; set; } = 0.30;
    public double CloudWeight { get; set; } = 0.15;
    public double TemperatureLow { get; set; } = 10.0;
    public double TemperatureIdeal { get; set; } = 18.0;
    public double TemperatureWarm { get; set; } = 28.0;
    public double TemperatureHot { get; set; } = 36.0;
    public double WindModerate { get; set; } = 5.0;
    public double WindStrong { get; set; } = 10.0;
    public double RainProbabilityLow { get; set; } = 0.0;
    public double RainProbabilityHigh { get; set; } = 60.0;
    public double CloudLow { get; set; } = 40.0;
    public double CloudHigh { get; set; } = 100.0;
}
