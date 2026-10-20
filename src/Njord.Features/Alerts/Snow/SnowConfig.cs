namespace Njord.Features.Alerts.Snow;

public sealed class SnowConfig
{
    public const string SectionName = "Njord:Features:Weather:Snow";
    public bool Enabled { get; set; } = true;
}
