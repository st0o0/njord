using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.Derived.Tests;

public sealed class DerivedAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);
    private static readonly IReadOnlyList<int> Horizons = [3, 6, 12, 24];

    [Fact]
    public void Compute_returns_derived_result()
    {
        var analyzer = CreateAnalyzer();

        var result = analyzer.Compute(BuildConsensus());

        Assert.NotNull(result);
        Assert.Equal(new LocationId("lucerne"), result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var analyzer = CreateAnalyzer(enabled: false);

        var result = analyzer.Compute(BuildConsensus());

        Assert.Null(result);
    }

    private static DerivedAnalyzer CreateAnalyzer(bool enabled = true) =>
        new(
            new DerivedResultComputer(Parameters),
            Options.Create(new DerivedOptions { Enabled = enabled }),
            Options.Create(new NjordOptions { Horizons = [.. Horizons] }));

    private static ConsensusSnapshot BuildConsensus()
    {
        var snap = ModelSnapshot.Empty;
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne");
    }
}
