using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Trends;

namespace Njord.Trends;

public sealed class TrendAnalyzer
{
    private readonly TrendComputer _computer;

    public TrendAnalyzer(TrendComputer computer, IOptions<TrendOptions> options)
    {
        _computer = computer;
        Enabled = options.Value.Enabled;
    }

    public bool Enabled { get; }

    public TrendResult? Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous, TrendSettings settings)
    {
        if (!Enabled || previous is null)
            return null;

        return _computer.Compute(consensus, previous);
    }
}
