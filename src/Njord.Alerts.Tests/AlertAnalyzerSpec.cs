using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Configuration;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests;

public sealed class AlertAnalyzerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);
    private static readonly IOptions<AlertOptions> EnabledOptions = Options.Create(AppSettingsDefaults.Options<AlertOptions>());

    [Fact]
    public void Compute_returns_no_active_alerts_for_normal_conditions()
    {
        var alerts = CreateAnalyzer().Compute(Day(), Defaults, Now);

        Assert.NotNull(alerts);
        Assert.All(alerts, a => Assert.Equal(AlertSeverity.None, a.Severity));
    }

    [Fact]
    public void Compute_returns_null_when_disabled()
    {
        var alerts = CreateAnalyzer(enabled: false).Compute(Day(), Defaults, Now);

        Assert.Null(alerts);
    }

    [Fact]
    public void Compute_returns_one_alert_per_alert_type_in_order()
    {
        var alerts = CreateAnalyzer().Compute(Day(), Defaults, Now);

        Assert.NotNull(alerts);
        Assert.Equal(14, alerts.Count);
        Assert.Equal(AllTypes(), alerts.Select(a => a.Type));
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
        var forecast = Uniform(p => p with { Temperature = -3.0 });
        var lenient = AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [-50, -60, -70]).ToSettings();

        var before = analyzer.Compute(forecast, Defaults, Now);
        var after = analyzer.Compute(forecast, lenient, Now);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(AlertSeverity.Yellow, before.Single(a => a.Type == AlertType.Frost).Severity);
        Assert.Equal(AlertSeverity.None, after.Single(a => a.Type == AlertType.Frost).Severity);
    }

    [Fact]
    public void Compute_evaluates_the_window_from_the_passed_time()
    {
        var forecast = Hourly(48, (h, p) => p with { Temperature = h >= 30 ? -5.0 : 10.0 });
        var analyzer = CreateAnalyzer();

        var early = analyzer.Compute(forecast, Defaults, Now);
        var late = analyzer.Compute(forecast, Defaults, Now.AddHours(10));

        Assert.NotNull(early);
        Assert.NotNull(late);
        Assert.Equal(AlertSeverity.None, early.Single(a => a.Type == AlertType.Frost).Severity);
        Assert.Equal(AlertSeverity.Orange, late.Single(a => a.Type == AlertType.Frost).Severity);
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
        var forward = new AlertAnalyzer(AllTypes().Select(t => new FixedRule(t)), EnabledOptions);
        var reverse = new AlertAnalyzer(AllTypes().Reverse().Select(t => new FixedRule(t)), EnabledOptions);

        var forwardResult = forward.Compute(Day(), Defaults, Now);
        var reverseResult = reverse.Compute(Day(), Defaults, Now);

        Assert.NotNull(forwardResult);
        Assert.NotNull(reverseResult);
        Assert.Equal(AllTypes(), forwardResult.Select(a => a.Type));
        Assert.Equal(AllTypes(), reverseResult.Select(a => a.Type));
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

    private static AlertType[] AllTypes() => Enum.GetValues<AlertType>();

    private static AlertAnalyzer CreateAnalyzer(bool enabled = true) => FeatureServices.Alerts(Time, enabled);

    private sealed class FixedRule(AlertType type) : IAlertRule
    {
        public AlertType Type => type;

        public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now) =>
            Alert.None(type);
    }
}
