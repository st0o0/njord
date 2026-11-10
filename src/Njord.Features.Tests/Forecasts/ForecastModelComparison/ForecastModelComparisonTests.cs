using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Configuration;
using Njord.Core.Forecast;
using Njord.Features.Forecasts.ForecastModelComparison;
using Njord.Messages.Results.Forecasts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Forecasts.ForecastModelComparison;

public sealed class ForecastModelComparisonTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static Features.Forecasts.ForecastModelComparison.ForecastModelComparison Rule() =>
        new(Opts(new ForecastModelComparisonConfig { Enabled = true }),
            Opts(new ActiveModels { ForecastModels = ["model_a", "model_b"] }));

    private static DomainForecast<ForecastPoint> ModelForecast(int hours = 48, double temp = 20.0) =>
        new(DomainKind.Forecast, Now, TimeResolution.Hourly,
            [.. Enumerable.Range(0, hours).Select(h =>
                new ForecastPoint(Now.AddHours(h), Temperature: temp, Precipitation: 0.0))]);

    [Fact]
    public void Project_returns_null_when_fewer_than_two_models()
    {
        var weather = Day();
        var forecast = ForecastOnly(weather);

        var result = Run(Rule(), Now, forecast);

        Assert.Null(result);
    }

    [Fact]
    public void Project_returns_null_when_one_model()
    {
        var forecast = ForecastOnly(Day())
            .WithForecastModel("model_a", ModelForecast());

        var result = Run(Rule(), Now, forecast);

        Assert.Null(result);
    }

    [Fact]
    public void Project_returns_input_when_two_models_present()
    {
        var forecast = ForecastOnly(Day())
            .WithForecastModel("model_a", ModelForecast())
            .WithForecastModel("model_b", ModelForecast(temp: 22.0));

        var result = Run(Rule(), Now, forecast);

        Assert.NotNull(result);
        var comparison = Assert.IsType<ForecastModelComparisonResult>(result, exactMatch: false);
        Assert.NotEmpty(comparison.Days);
    }

    [Fact]
    public void Evaluate_returns_result_with_correct_day_count()
    {
        var forecast = ForecastOnly(Day())
            .WithForecastModel("model_a", ModelForecast(hours: 72))
            .WithForecastModel("model_b", ModelForecast(hours: 72, temp: 25.0));

        var result = Run(Rule(), Now, forecast);

        Assert.NotNull(result);
        var comparison = Assert.IsType<ForecastModelComparisonResult>(result, exactMatch: false);
        Assert.True(comparison.Days.Length >= 2);
        foreach (var day in comparison.Days)
        {
            Assert.Equal(2, day.Models.Length);
        }
    }

    [Fact]
    public void Enabled_is_false_when_fewer_than_two_forecast_models()
    {
        var rule = new Features.Forecasts.ForecastModelComparison.ForecastModelComparison(
            Opts(new ForecastModelComparisonConfig { Enabled = true }),
            Opts(new ActiveModels { ForecastModels = ["only_one"] }));

        Assert.False(rule.Enabled);
    }

    [Fact]
    public void Enabled_is_false_when_config_disabled()
    {
        var rule = new Features.Forecasts.ForecastModelComparison.ForecastModelComparison(
            Opts(new ForecastModelComparisonConfig { Enabled = false }),
            Opts(new ActiveModels { ForecastModels = ["a", "b"] }));

        Assert.False(rule.Enabled);
    }
}
