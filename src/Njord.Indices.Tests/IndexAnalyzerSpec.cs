using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Indices;
using Njord.Weather;

namespace Njord.Indices.Tests;

public sealed class IndexAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact]
    public void Compute_returns_index_result()
    {
        var analyzer = CreateAnalyzer();

        var result = analyzer.Compute(BuildConsensus(), null);

        Assert.NotNull(result);
        Assert.Equal("lucerne", result.Location);
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var analyzer = CreateAnalyzer(enabled: false);

        var result = analyzer.Compute(BuildConsensus(), null);

        Assert.Null(result);
    }

    private static IndexAnalyzer CreateAnalyzer(bool enabled = true)
    {
        var indexComputer = new IndexComputer(Parameters, Time);
        var indexOptions = new IndexOptions();
        return new IndexAnalyzer(indexComputer, indexOptions, ["lucerne"], enabled);
    }

    private static ConsensusSnapshot BuildConsensus()
    {
        var snap = ModelSnapshot.Empty;
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne");
    }
}
