namespace Njord.Features.Alerts.Ice;

public sealed class IceConfig
{
    public const string SectionName = "Njord:Features:Weather:Ice";
    public bool Enabled { get; set; } = true;
    public double Threshold { get; set; } = 2.0;
}
