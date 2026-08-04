namespace Njord.Configuration;

public sealed class SensorOptions
{
    public bool Enabled { get; set; } = true;
    public int StalenessSeconds { get; set; } = 7200;
}
