using Njord.Compute.Analysis;

namespace Njord.Derived;

public sealed class DerivedAnalyzer
{
    private readonly DerivedResultComputer _computer;
    private readonly bool _enabled;

    public DerivedAnalyzer(DerivedResultComputer computer, bool enabled = true)
    {
        _computer = computer;
        _enabled = enabled;
    }

    public bool Enabled => _enabled;

    public DerivedResult? Compute(IDerivedInput input)
    {
        if (!Enabled)
            return null;

        return _computer.Compute(input.Consensus, input.Horizons);
    }
}
