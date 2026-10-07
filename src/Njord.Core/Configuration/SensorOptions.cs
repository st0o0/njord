namespace Njord.Core.Configuration;

public sealed class SensorOptions
{
    public const string SectionName = "Njord:Sensors";

    public bool Enabled { get; set; } = true;
    public int StalenessSeconds { get; set; } = 7200;
}
