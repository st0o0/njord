namespace Njord.Core.Configuration;

public sealed class MarineOptions
{
    public const string SectionName = "Njord:Marine";

    public int ForecastDays { get; set; } = 8;
}
