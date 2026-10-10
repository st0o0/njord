using Njord.Compute.Configuration;
using Njord.Compute.Analysis;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.History;
using Njord.Weather;

namespace Njord.History.Tests;

public sealed class HistoryTrackerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact]
    public void Analyze_returns_null_when_disabled()
    {
        var tracker = CreateTracker(enabled: false);
        var history = new ForecastHistory(30);

        var result = tracker.Analyze(history, ModelSnapshot.Empty, "lucerne", Parameters, Time);

        Assert.Null(result);
    }

    [Fact]
    public void Analyze_returns_result_when_enabled_with_sufficient_data()
    {
        var tracker = CreateTracker(enabled: true);
        var history = new ForecastHistory(30);

        var result = tracker.Analyze(history, ModelSnapshot.Empty, "lucerne", Parameters, Time);

        Assert.NotNull(result);
        Assert.Equal("lucerne", result.Location);
    }

    [Fact]
    public void Enabled_reflects_options()
    {
        Assert.Multiple(
            () => Assert.True(CreateTracker(enabled: true).Enabled),
            () => Assert.False(CreateTracker(enabled: false).Enabled));
    }

    private static HistoryTracker CreateTracker(bool enabled)
    {
        var options = Options.Create(new HistoryOptions { Enabled = enabled });
        var computer = new HistoryComputer();
        return new HistoryTracker(options, computer);
    }
}
