namespace Njord.Core.Configuration;

public sealed class ForecastOptions : IConfigSection
{
    private const string SectionName = "Njord:Forecast";
    public const string DefaultModel = "ecmwf_ifs025_ensemble_mean";
    static string IConfigSection.SectionName => SectionName;

    // The first entry is the primary model; null means the default model (the binder appends to a list default).
    public string[]? Models { get; set; }

    public string[] EffectiveModels => Models ?? [DefaultModel];

    public string PrimaryModel => EffectiveModels is [var first, ..] ? first : DefaultModel;

    // Removed key; only bound so that validation can reject it with a pointer to Models.
    public string? Model { get; set; }

    public int ForecastDays { get; set; } = 4;
}
