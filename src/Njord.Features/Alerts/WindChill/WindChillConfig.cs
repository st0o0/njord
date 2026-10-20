namespace Njord.Features.Alerts.WindChill;

public sealed class WindChillConfig
{
    public const string SectionName = "Njord:Features:Weather:WindChill";
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [-10, -20, -30];
}
