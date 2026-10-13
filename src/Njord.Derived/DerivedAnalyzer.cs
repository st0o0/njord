using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Derived;

namespace Njord.Derived;

public sealed class DerivedAnalyzer
{
    private readonly DerivedResultComputer _computer;

    public DerivedAnalyzer(DerivedResultComputer computer, IOptions<DerivedOptions> options)
    {
        _computer = computer;
        Enabled = options.Value.Enabled;
    }

    public bool Enabled { get; }

    public DerivedResult? Compute(ConsensusSnapshot consensus, DerivedSettings settings)
    {
        if (!Enabled)
            return null;

        return _computer.Compute(consensus, settings);
    }
}
