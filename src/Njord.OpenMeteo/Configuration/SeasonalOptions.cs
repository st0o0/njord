using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SeasonalOptions : IConfigSection
{
    private const string SectionName = "Njord:Seasonal";
    static string IConfigSection.SectionName => SectionName;

    // Months after the current month that are kept from the seasonal forecast.
    public int ForecastMonths { get; set; } = 3;

    // Seasonal model sent as the models query parameter (EC46 and SEAS5 blend).
    public string Model { get; set; } = "ecmwf_seasonal_ensemble_mean_seamless";
}
