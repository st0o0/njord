using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Derived;
using Njord.Weather;

namespace Njord.Derived.Tests;

public sealed class DerivedAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private sealed record TestDerivedInput(ConsensusSnapshot Consensus, IReadOnlyList<int> Horizons) : IDerivedInput;

    [Fact]
    public void Compute_returns_derived_result()
    {
        var computer = new DerivedResultComputer(Parameters);
        var analyzer = new DerivedAnalyzer(computer);
        var input = new TestDerivedInput(BuildConsensus(), [3, 6, 12, 24]);

        var result = analyzer.Compute(input);

        Assert.NotNull(result);
        Assert.Equal("lucerne", result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var computer = new DerivedResultComputer(Parameters);
        var analyzer = new DerivedAnalyzer(computer, enabled: false);
        var input = new TestDerivedInput(BuildConsensus(), [3, 6, 12, 24]);

        var result = analyzer.Compute(input);

        Assert.Null(result);
    }

    private static ConsensusSnapshot BuildConsensus()
    {
        var snap = ModelSnapshot.Empty;
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne");
    }
}
