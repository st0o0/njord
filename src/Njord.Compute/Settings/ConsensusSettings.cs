namespace Njord.Compute.Settings;

public sealed record ConsensusSettings(ConsensusMethod Method, double TrimPercent, int MinQuorum)
{
    public static ConsensusSettings Default { get; } = new(ConsensusMethod.Median, 0.1, 2);
}
