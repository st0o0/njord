namespace Njord.Features.Alerts.Humidity;

public sealed class HumidityConfig
{
    public const string SectionName = "Njord:Features:Weather:Humidity";
    public bool Enabled { get; set; } = true;
    public double[] Thresholds { get; set; } = [16, 21, 24];
}
