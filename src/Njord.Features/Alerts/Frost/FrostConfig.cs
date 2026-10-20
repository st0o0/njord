namespace Njord.Features.Alerts.Frost;

public sealed class FrostConfig
{
    public const string SectionName = "Njord:Features:Weather:Frost";
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [0, -5, -15];
}
