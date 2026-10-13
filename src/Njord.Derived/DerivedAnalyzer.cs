using Microsoft.Extensions.Options;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Core.Configuration.Derived;

namespace Njord.Derived;

public sealed class DerivedAnalyzer
{
    private readonly DerivedResultComputer _computer;
    private readonly IReadOnlyList<int> _horizons;

    public DerivedAnalyzer(
        DerivedResultComputer computer,
        IOptions<DerivedOptions> derivedOptions,
        IOptions<NjordOptions> njordOptions)
    {
        _computer = computer;
        _horizons = [.. njordOptions.Value.Horizons];
        Enabled = derivedOptions.Value.Enabled;
    }

    public bool Enabled { get; }

    public DerivedResult? Compute(ConsensusSnapshot consensus)
    {
        if (!Enabled)
            return null;

        return _computer.Compute(consensus, _horizons);
    }
}
