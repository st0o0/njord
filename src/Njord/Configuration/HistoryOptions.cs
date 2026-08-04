namespace Njord.Configuration;

public sealed class HistoryOptions
{
    public bool Enabled { get; set; } = false;
    public int RetentionDays { get; set; } = 30;
    public int MinSampleSize { get; set; } = 48;
    public int SnapshotInterval { get; set; } = 100;
}
