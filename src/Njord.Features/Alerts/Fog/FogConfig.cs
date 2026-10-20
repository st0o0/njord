namespace Njord.Features.Alerts.Fog;

public sealed class FogConfig
{
    public const string SectionName = "Njord:Features:Weather:Fog";
    public bool Enabled { get; set; } = true;
    public int PersistentHours { get; set; } = 4;
}
