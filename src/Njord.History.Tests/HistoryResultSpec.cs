using Njord.Compute.Configuration;
using Njord.Compute.Analysis;
using Microsoft.Extensions.Time.Testing;
using Njord.History;
using Njord.History;
using Njord.Weather;

namespace Njord.History.Tests;

public sealed class HistoryResultSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;

    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(
        ["Weather"], [], []);

    [Fact]
    public void Compute_with_empty_history_produces_null_metrics()
    {
        var history = new ForecastHistory(30);
        var snapshot = ModelSnapshot.Empty;

        var result = new HistoryComputer().Compute(history, snapshot, "lucerne", Parameters, Time, new HistoryOptions());

        Assert.Equal("lucerne", result.Location);
        Assert.Empty(result.Mae7d);
        Assert.Null(result.SeasonalBest);
    }

}
