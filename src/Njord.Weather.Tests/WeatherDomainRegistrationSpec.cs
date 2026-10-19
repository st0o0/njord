using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests;

public sealed class WeatherDomainRegistrationSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    [Fact]
    public void Add_weather_domain_registers_exactly_one_rule_per_weather_alert_type_and_the_handler()
    {
        using var provider = FeatureServices.Build(Time, services => services.AddAlertOptions(AppSettingsDefaults.Configuration()).AddWeatherDomain());

        var rules = provider.GetServices<IAlertRule<DomainForecast<WeatherPoint>>>().ToList();
        var handler = Assert.Single(provider.GetServices<IDomainHandler>());

        Assert.Equal(Enum.GetValues<AlertType>().Where(t => t.IsWeather()), rules.Select(r => r.Type).Order());
        Assert.IsType<WeatherAlertHandler>(handler);
    }

    [Fact]
    public void Rules_evaluate_against_the_options_passed_with_each_call()
    {
        var rule = FeatureServices.WeatherRules(Time).Single(r => r.Type == AlertType.Frost);
        var forecast = Uniform(p => p with { Temperature = -3.0 });
        var lenient = AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [-50, -60, -70]);

        Assert.Equal(AlertSeverity.Yellow, rule.Evaluate(forecast, Defaults, Now).Severity);
        Assert.Equal(AlertSeverity.None, rule.Evaluate(forecast, lenient, Now).Severity);
    }

    [Fact]
    public void Rules_evaluate_the_window_from_the_passed_time()
    {
        var rule = FeatureServices.WeatherRules(Time).Single(r => r.Type == AlertType.Frost);
        var forecast = Hourly(48, (h, p) => p with { Temperature = h >= 30 ? -5.0 : 10.0 });

        Assert.Equal(AlertSeverity.None, rule.Evaluate(forecast, Defaults, Now).Severity);
        Assert.Equal(AlertSeverity.Orange, rule.Evaluate(forecast, Defaults, Now.AddHours(10)).Severity);
    }
}
