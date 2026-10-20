namespace Njord.Core.Configuration;

public sealed class WeatherOptions
{
    public const string SectionName = "Njord:Weather";

    public bool IncludeSolar { get; set; }

    public string Model { get; set; } = "ecmwf_ifs025_ensemble_mean";

    public int ForecastDays { get; set; } = 4;
}
