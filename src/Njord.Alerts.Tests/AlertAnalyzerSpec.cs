using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Configuration;
using Njord.Consensus;
using Njord.Consensus.Settings;
using Njord.Core.Configuration;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using Njord.Model;
using Njord.Forecast;
using WeatherForecast = Njord.Forecast.Forecast;

namespace Njord.Alerts.Tests;

public sealed class AlertAnalyzerSpec
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeTimeProvider Time = new(T0);
    private static readonly ParameterDef Temperature = ParameterRegistry.GetByApiName("temperature_2m")!;
    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(["Weather"], [], []);
    private static readonly AlertSettings DefaultSettings = AppSettingsDefaults.Options<AlertOptions>().ToSettings();

    [Fact]
    public void Compute_returns_alert_result_for_normal_conditions()
    {
        var analyzer = CreateAnalyzer();
        var consensus = BuildConsensus((Temperature, 20.0));

        var result = analyzer.Compute(consensus, DefaultSettings);

        Assert.NotNull(result);
        Assert.All(result.Alerts, a => Assert.Equal(AlertSeverity.None, a.Severity));
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var analyzer = CreateAnalyzer(enabled: false);
        var consensus = BuildConsensus((Temperature, 20.0));

        var result = analyzer.Compute(consensus, DefaultSettings);

        Assert.Null(result);
    }

    [Fact]
    public void Compute_returns_all_alert_types()
    {
        var analyzer = CreateAnalyzer();
        var consensus = BuildConsensus((Temperature, 20.0));

        var result = analyzer.Compute(consensus, DefaultSettings);

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

    [Fact]
    public void Compute_uses_the_settings_passed_with_each_computation()
    {
        var analyzer = CreateAnalyzer();
        var consensus = AnalysisFixture.Create().For(AnalysisFixture.Lucerne).Consensus;
        var lenient = AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [-50, -60, -70]).ToSettings();

        var before = analyzer.Compute(consensus, DefaultSettings);
        var after = analyzer.Compute(consensus, lenient);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.NotEqual(AlertSeverity.None, before.Alerts.Single(a => a.Type == AlertType.Frost).Severity);
        Assert.Equal(AlertSeverity.None, after.Alerts.Single(a => a.Type == AlertType.Frost).Severity);
    }

    [Fact]
    public void Missing_rule_fails_fast_at_construction()
    {
        var rules = AllTypes().Where(t => t != AlertType.Fog).Select(t => new FixedRule(t));

        var error = Assert.Throws<InvalidOperationException>(() => new AlertAnalyzer(rules, EnabledOptions));

        Assert.Contains(nameof(AlertType.Fog), error.Message);
    }

    [Fact]
    public void Duplicate_rule_fails_fast_at_construction()
    {
        var rules = AllTypes().Select(t => new FixedRule(t)).Append(new FixedRule(AlertType.Heat));

        var error = Assert.Throws<InvalidOperationException>(() => new AlertAnalyzer(rules, EnabledOptions));

        Assert.Contains(nameof(AlertType.Heat), error.Message);
    }

    [Fact]
    public void Reverse_registration_keeps_alert_type_order()
    {
        var consensus = BuildConsensus((Temperature, 20.0));
        var forward = new AlertAnalyzer(AllTypes().Select(t => new FixedRule(t)), EnabledOptions);
        var reverse = new AlertAnalyzer(AllTypes().Reverse().Select(t => new FixedRule(t)), EnabledOptions);

        var forwardResult = forward.Compute(consensus, DefaultSettings);
        var reverseResult = reverse.Compute(consensus, DefaultSettings);

        Assert.NotNull(forwardResult);
        Assert.NotNull(reverseResult);
        Assert.Equal(AllTypes(), forwardResult.Alerts.Select(a => a.Type));
        Assert.Equal(AllTypes(), reverseResult.Alerts.Select(a => a.Type));
    }

    [Fact]
    public void Add_alerts_registers_the_analyzer_and_one_rule_per_alert_type()
    {
        using var provider = FeatureServices.Build(Time, services => services.AddAlerts(AppSettingsDefaults.Configuration()));

        var rules = provider.GetServices<IAlertRule>().ToList();
        var analyzer = provider.GetRequiredService<AlertAnalyzer>();

        Assert.Equal(AllTypes(), rules.Select(r => r.Type).Order());
        Assert.True(analyzer.Enabled);
    }

    private static readonly IOptions<AlertOptions> EnabledOptions = Options.Create(AppSettingsDefaults.Options<AlertOptions>());

    private static AlertType[] AllTypes() => Enum.GetValues<AlertType>();

    private static AlertAnalyzer CreateAnalyzer(bool enabled = true) => FeatureServices.Alerts(Time, enabled);

    private static LocationConsensus BuildConsensus(params (ParameterDef Param, double Value)[] hourlyValues)
    {
        var points = new List<ForecastPoint>();
        for (var h = 0; h < 24; h++)
        {
            var values = new Dictionary<ParameterDef, ForecastValue>();
            foreach (var (param, value) in hourlyValues)
                values[param] = value;
            points.Add(new ForecastPoint(T0.AddHours(h), values));
        }

        var forecast = new WeatherForecast(new WeatherModel("m1"), "lucerne", new CycleId(T0),
            new ForecastSeries(points), DailyForecastSeries.Empty);
        var snap = ModelForecasts.Empty.Update(forecast);
        return new ConsensusBuilder(Parameters, Time).Create(snap, "lucerne", ConsensusSettings.Default);
    }

    private sealed class FixedRule(AlertType type) : IAlertRule
    {
        public AlertType Type => type;

        public Alert Evaluate(LocationConsensus consensus, AlertSettings settings) => Alert.None(type);
    }
}
