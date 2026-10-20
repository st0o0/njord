namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropConfig
{
    public const string SectionName = "Njord:Features:Weather:PressureDrop";
    public bool Enabled { get; set; } = true;
    public double Threshold { get; set; } = 5.0;
    public double SevereThreshold { get; set; } = 10.0;
}
