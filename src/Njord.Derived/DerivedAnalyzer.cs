using Njord.Compute.Analysis;

namespace Njord.Derived;

public sealed class DerivedAnalyzer
{
    private readonly DerivedResultComputer _computer;
    private readonly IReadOnlyList<int> _horizons;
    private readonly bool _enabled;

    public DerivedAnalyzer(DerivedResultComputer computer, IReadOnlyList<int> horizons, bool enabled = true)
    {
        _computer = computer;
        _horizons = horizons;
        _enabled = enabled;
    }

    public bool Enabled => _enabled;

    public DerivedResult? Compute(ConsensusSnapshot consensus)
    {
        if (!Enabled)
            return null;

        return _computer.Compute(consensus, _horizons);
    }
}
