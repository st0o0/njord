namespace Njord.Features.Alerts.TropicalNight;

public sealed class TropicalNightConfig
{
    public const string SectionName = "Njord:Features:Weather:TropicalNight";
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [20, 23, 25];
}
