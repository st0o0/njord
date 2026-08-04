namespace Njord.Configuration;

public sealed class ConsensusOptions
{
    public bool Enabled { get; set; } = true;
    public string Method { get; set; } = "Median";
    public double TrimPercent { get; set; } = 0.1;
}
