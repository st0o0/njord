using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Trends;
using Njord.Weather;

namespace Njord.Trends.Tests;

public sealed class TrendAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    private sealed record TestTrendInput(ConsensusSnapshot Consensus, ConsensusSnapshot? Previous) : ITrendInput;

    [Fact]
    public void Compute_returns_null_when_previous_is_null()
    {
        var computer = new TrendComputer();
        var analyzer = new TrendAnalyzer(computer);
        var input = new TestTrendInput(BuildConsensus(), null);

        var result = analyzer.Compute(input);

        Assert.Null(result);
    }

    [Fact]
    public void Compute_returns_trend_result_with_previous()
    {
        var computer = new TrendComputer();
        var analyzer = new TrendAnalyzer(computer);
        var current = BuildConsensus();
        var previous = BuildConsensus();
        var input = new TestTrendInput(current, previous);

        var result = analyzer.Compute(input);

        Assert.NotNull(result);
        Assert.Equal("lucerne", result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var computer = new TrendComputer();
        var analyzer = new TrendAnalyzer(computer, enabled: false);
        var input = new TestTrendInput(BuildConsensus(), BuildConsensus());

        var result = analyzer.Compute(input);

        Assert.Null(result);
    }

    private static ConsensusSnapshot BuildConsensus()
    {
        var snap = ModelSnapshot.Empty;
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne");
    }
}
