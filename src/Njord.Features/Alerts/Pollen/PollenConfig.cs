namespace Njord.Features.Alerts.Pollen;

public sealed class PollenConfig
{
    public const string SectionName = "Njord:Features:AirQuality:Pollen";
    public bool Enabled { get; set; } = true;
}
