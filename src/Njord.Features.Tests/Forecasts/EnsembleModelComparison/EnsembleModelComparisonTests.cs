using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Forecasts.EnsembleModelComparison;
using Njord.Messages.Results.Forecasts;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Forecasts.EnsembleModelComparison;

public sealed class EnsembleModelComparisonTests
{
    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static Features.Forecasts.EnsembleModelComparison.EnsembleModelComparison Rule() =>
        new(Opts(new EnsembleModelComparisonConfig { Enabled = true }));

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
            .WithEnsembleModel("ensemble_a", ModelForecast());

        var result = Run(Rule(), Now, forecast);

        Assert.Null(result);
    }

    [Fact]
    public void Project_returns_input_when_two_models_present()
    {
        var forecast = ForecastOnly(Day())
            .WithEnsembleModel("ensemble_a", ModelForecast())
            .WithEnsembleModel("ensemble_b", ModelForecast(temp: 22.0));

        var result = Run(Rule(), Now, forecast);

        Assert.NotNull(result);
        var comparison = Assert.IsType<EnsembleModelComparisonResult>(result, exactMatch: false);
        Assert.NotEmpty(comparison.Days);
    }

    [Fact]
    public void Evaluate_returns_result_with_correct_day_count()
    {
        var forecast = ForecastOnly(Day())
            .WithEnsembleModel("ensemble_a", ModelForecast(hours: 72))
            .WithEnsembleModel("ensemble_b", ModelForecast(hours: 72, temp: 25.0));

        var result = Run(Rule(), Now, forecast);

        Assert.NotNull(result);
        var comparison = Assert.IsType<EnsembleModelComparisonResult>(result, exactMatch: false);
        Assert.True(comparison.Days.Length >= 2);
        foreach (var day in comparison.Days)
        {
            Assert.Equal(2, day.Models.Length);
        }
    }
}
