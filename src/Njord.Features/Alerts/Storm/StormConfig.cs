namespace Njord.Features.Alerts.Storm;

public sealed class StormConfig
{
    public const string SectionName = "Njord:Features:Weather:Storm";
    public bool Enabled { get; set; } = true;
    public double[] GustThresholds { get; set; } = [17, 25, 33];
}
