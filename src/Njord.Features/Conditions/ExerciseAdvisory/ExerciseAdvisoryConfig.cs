using Njord.Core.Configuration;

namespace Njord.Features.Conditions.ExerciseAdvisory;

public sealed class ExerciseAdvisoryConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Exercise";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double SafeScore { get; set; } = 70.0;
    public double CautionScore { get; set; } = 45.0;
    public double ReasonThreshold { get; set; } = 0.5;
    public double Pm25Weight { get; set; } = 0.4;
    public double OzoneWeight { get; set; } = 0.2;
    public double TemperatureWeight { get; set; } = 0.2;
    public double HumidityWeight { get; set; } = 0.1;
    public double UvWeight { get; set; } = 0.1;
    public double Pm25Low { get; set; } = 15.0;
    public double Pm25High { get; set; } = 55.0;
    public double OzoneLow { get; set; } = 100.0;
    public double OzoneHigh { get; set; } = 180.0;
    public double TemperatureCold { get; set; } = -5.0;
    public double TemperatureCool { get; set; } = 8.0;
    public double TemperatureWarm { get; set; } = 22.0;
    public double TemperatureHot { get; set; } = 35.0;
    public double HumidityLow { get; set; } = 70.0;
    public double HumidityHigh { get; set; } = 100.0;
    public double UvModerate { get; set; } = 5.0;
    public double UvHigh { get; set; } = 11.0;
}
