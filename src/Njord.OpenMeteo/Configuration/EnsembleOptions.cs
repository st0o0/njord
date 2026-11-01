using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class EnsembleOptions : IConfigSection, IHasArrayDefaults
{
    private const string SectionName = "Njord:Ensemble";
    public static readonly string[] DefaultModels = ["ecmwf_ifs025_ensemble_mean", "ncep_gefs025_ensemble_mean", "google_weathernext2_ensemble_mean"];
    static string IConfigSection.SectionName => SectionName;

    public string[] Models { get; set; } = [];

    public int ForecastDays { get; set; } = 4;

    public void ApplyDefaults()
    {
        if (Models is { Length: 0 })
        {
            Models = [.. DefaultModels];
        }
    }
}
