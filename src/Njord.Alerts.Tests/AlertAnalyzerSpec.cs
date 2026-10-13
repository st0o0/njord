using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Compute.Analysis;
using Njord.Compute.Configuration;
using Njord.Weather;

namespace Njord.Alerts.Tests;

public sealed class AlertAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ResolvedParameterSet Parameters = ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact]
    public void Compute_returns_alert_result_for_normal_conditions()
    {
        var analyzer = CreateAnalyzer();
        var consensus = BuildConsensus((Temperature, 20.0));

        var result = analyzer.Compute(consensus);

        Assert.NotNull(result);
        Assert.All(result.Alerts, a => Assert.Equal(AlertSeverity.None, a.Severity));
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var analyzer = CreateAnalyzer(enabled: false);
        var consensus = BuildConsensus((Temperature, 20.0));

        var result = analyzer.Compute(consensus);

        Assert.Null(result);
    }

    [Fact]
    public void Compute_returns_all_alert_types()
    {
        var analyzer = CreateAnalyzer();
        var consensus = BuildConsensus((Temperature, 20.0));

        var result = analyzer.Compute(consensus);

        Assert.NotNull(result);
        Assert.Equal(14, result.Alerts.Count);
        Assert.Equal(new LocationId("lucerne"), result.Location);
    }

    [Fact]
    public void Enabled_reflects_options()
    {
        Assert.Multiple(
            () => Assert.True(CreateAnalyzer(enabled: true).Enabled),
            () => Assert.False(CreateAnalyzer(enabled: false).Enabled));
    }

    private static AlertAnalyzer CreateAnalyzer(bool enabled = true)
    {
        var options = Options.Create(new AlertOptions { Enabled = enabled });
        return new AlertAnalyzer(options, Time);
    }

    private static ConsensusSnapshot BuildConsensus(params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var values = new Dictionary<ParameterDef, double?>();
            foreach (var (param, value) in hourlyValues)
                values[param] = value;
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }

        var forecast = new Forecast(new WeatherModel("m1"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var snap = ModelSnapshot.Empty.Update(forecast);
        return new ConsensusSnapshotFactory(Parameters, Time).Create(snap, "lucerne");
    }
}
