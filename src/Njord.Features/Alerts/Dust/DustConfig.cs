namespace Njord.Features.Alerts.Dust;

public sealed class DustConfig
{
    public const string SectionName = "Njord:Features:AirQuality:Dust";
    public bool Enabled { get; set; }
    public double[] Thresholds { get; set; } = [50, 150, 500];
}
