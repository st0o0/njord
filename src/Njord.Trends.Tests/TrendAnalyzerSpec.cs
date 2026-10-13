using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Compute.Settings;
using Njord.Core.Configuration.Trends;
using Njord.Weather;

namespace Njord.Trends.Tests;

public sealed class TrendAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact]
    public void Compute_returns_null_when_previous_is_null()
    {
        var computer = new TrendComputer();
        var analyzer = new TrendAnalyzer(computer, Options.Create(new TrendOptions { Enabled = true }));

        var result = analyzer.Compute(BuildConsensus(), null, TrendSettings.Default);

        Assert.Null(result);
    }

    [Fact]
    public void Compute_returns_trend_result_with_previous()
    {
        var computer = new TrendComputer();
        var analyzer = new TrendAnalyzer(computer, Options.Create(new TrendOptions { Enabled = true }));

        var result = analyzer.Compute(BuildConsensus(), BuildConsensus(), TrendSettings.Default);

        Assert.NotNull(result);
        Assert.Equal(new LocationId("lucerne"), result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var computer = new TrendComputer();
        var analyzer = new TrendAnalyzer(computer, Options.Create(new TrendOptions { Enabled = false }));

        var result = analyzer.Compute(BuildConsensus(), BuildConsensus(), TrendSettings.Default);

        Assert.Null(result);
    }

    private static ConsensusSnapshot BuildConsensus()
    {
        var snap = ModelSnapshot.Empty;
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne", ConsensusSettings.Default);
    }
}
