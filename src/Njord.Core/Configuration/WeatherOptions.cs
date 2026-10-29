namespace Njord.Core.Configuration;

public sealed class WeatherOptions : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Weather";
    public const string DefaultModel = "ecmwf_ifs025_ensemble_mean";
    static string IConfigSection.SectionName => SectionName;

    public string[] Models { get; set; } = [];

    public int ForecastDays { get; set; } = 4;

    public int PastHours { get; set; } = 0;

    public void ApplyDefaults()
    {
        if (Models is { Length: 0 }) Models = [DefaultModel];
    }
}
