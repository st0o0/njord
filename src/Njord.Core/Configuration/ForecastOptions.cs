namespace Njord.Core.Configuration;

public sealed class ForecastOptions : IConfigSection
{
    private const string SectionName = "Njord:Forecast";
    public const string DefaultModel = "ecmwf_ifs025_ensemble_mean";
    static string IConfigSection.SectionName => SectionName;

    // The first entry is the primary model; empty is filled with DefaultModel by PostConfigure (the binder appends to a list default).
    public string[] Models { get; set; } = [];

    public int ForecastDays { get; set; } = 4;

    public int PastHours { get; set; } = 0;
}
