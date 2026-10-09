using Njord.Compute.Analysis;

namespace Njord.Trends;

public sealed class TrendAnalyzer
{
    private readonly TrendComputer _computer;
    private readonly bool _enabled;

    public TrendAnalyzer(TrendComputer computer, bool enabled = true)
    {
        _computer = computer;
        _enabled = enabled;
    }

    public bool Enabled => _enabled;

    public TrendResult? Compute(ITrendInput input)
    {
        if (!Enabled || input.Previous is null)
            return null;

        return _computer.Compute(input.Consensus, input.Previous);
    }
}
