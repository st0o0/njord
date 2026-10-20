namespace Njord.Core.Configuration;

public sealed class WeatherOptions : IConfigSection
{
    public const string SectionName = "Njord:Weather";
    static string IConfigSection.SectionName => SectionName;

    public string Model { get; set; } = "ecmwf_ifs025_ensemble_mean";

    public int ForecastDays { get; set; } = 4;
}
