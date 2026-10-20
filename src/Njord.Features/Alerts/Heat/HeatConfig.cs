namespace Njord.Features.Alerts.Heat;

public sealed class HeatConfig
{
    public const string SectionName = "Njord:Features:Weather:Heat";
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [30, 35, 40];
}
