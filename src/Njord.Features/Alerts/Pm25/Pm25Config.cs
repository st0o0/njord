namespace Njord.Features.Alerts.Pm25;

public sealed class Pm25Config
{
    public const string SectionName = "Njord:Features:AirQuality:Pm25";
    public bool Enabled { get; set; } = true;
}
