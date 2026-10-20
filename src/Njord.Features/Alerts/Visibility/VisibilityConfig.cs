namespace Njord.Features.Alerts.Visibility;

public sealed class VisibilityConfig
{
    public const string SectionName = "Njord:Features:Weather:Visibility";
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [1000, 200, 50];
}
