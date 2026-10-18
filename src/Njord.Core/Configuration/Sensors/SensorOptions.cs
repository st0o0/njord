namespace Njord.Core.Configuration.Sensors;

public sealed class SensorOptions
{
    public const string SectionName = "Njord:Sensors";

    public bool Enabled { get; set; }
    public int StalenessSeconds { get; set; }
}
