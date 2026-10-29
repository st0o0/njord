namespace Njord.Core.Configuration;

public sealed class ForecastOptions : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Forecast";
    static string IConfigSection.SectionName => SectionName;

    public string[] Models { get; set; } = [];

    public int ForecastDays { get; set; } = 4;

    public int PastHours { get; set; }

    public void ApplyDefaults()
    {
    }
}
