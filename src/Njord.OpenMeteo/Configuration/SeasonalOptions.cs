using Njord.Core.Configuration;

namespace Njord.OpenMeteo.Configuration;

public sealed class SeasonalOptions : IConfigSection
{
    private const string SectionName = "Njord:Seasonal";
    static string IConfigSection.SectionName => SectionName;

    // Months after the current month that are kept from the seasonal forecast.
    public int ForecastMonths { get; set; } = 3;

    // Optional seasonal model sent as the models query parameter; empty (default) leaves the API default.
    public string Model { get; set; } = "";
}
