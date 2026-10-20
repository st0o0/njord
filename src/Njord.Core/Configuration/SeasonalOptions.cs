namespace Njord.Core.Configuration;

public sealed class SeasonalOptions
{
    public const string SectionName = "Njord:Seasonal";

    // Months after the current month that are kept from the seasonal forecast.
    public int ForecastMonths { get; set; } = 3;
}
