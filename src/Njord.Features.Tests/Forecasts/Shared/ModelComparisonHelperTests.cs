using System.Collections.Immutable;
using Njord.Core;
using Njord.Core.Forecast;
using Njord.Features.Forecasts.Shared;

namespace Njord.Features.Tests.Forecasts.Shared;

public sealed class ModelComparisonHelperTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static DomainForecast<ForecastPoint> ModelForecast(DateTimeOffset start, int hours, double temp, double precip) =>
        new(DomainKind.Forecast, start, TimeResolution.Hourly,
            [.. Enumerable.Range(0, hours).Select(h =>
                new ForecastPoint(start.AddHours(h), Temperature: temp + h * 0.1, Precipitation: precip))]);

    [Fact]
    public void Two_models_with_overlapping_days_produce_comparison()
    {
        var models = ImmutableDictionary.CreateRange([
            KeyValuePair.Create("model_a", ModelForecast(_now, 48, 20.0, 1.0)),
            KeyValuePair.Create("model_b", ModelForecast(_now, 48, 22.0, 2.0)),
        ]);

        var result = ModelComparisonHelper.CompareDays(models, _now);

        Assert.NotEmpty(result);
        Assert.True(result.Length >= 2);
        foreach (var day in result)
        {
            Assert.Equal(2, day.Models.Length);
            Assert.Equal("model_a", day.Models[0].ModelId);
            Assert.Equal("model_b", day.Models[1].ModelId);
        }
    }

    [Fact]
    public void Past_only_data_returns_empty()
    {
        var yesterday = _now.AddDays(-2);
        var models = ImmutableDictionary.CreateRange([
            KeyValuePair.Create("model_a", ModelForecast(yesterday, 24, 20.0, 1.0)),
            KeyValuePair.Create("model_b", ModelForecast(yesterday, 24, 22.0, 2.0)),
        ]);

        var result = ModelComparisonHelper.CompareDays(models, _now);

        Assert.Empty(result);
    }

    [Fact]
    public void Null_temperature_and_precipitation_are_preserved()
    {
        var points = ImmutableArray.Create(new ForecastPoint(_now));
        var models = ImmutableDictionary.CreateRange([
            KeyValuePair.Create("model_a", new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, points)),
            KeyValuePair.Create("model_b", new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, points)),
        ]);

        var result = ModelComparisonHelper.CompareDays(models, _now);

        var item = Assert.Single(result);
        foreach (var model in item.Models)
        {
            Assert.Null(model.MaxTemperature);
            Assert.Null(model.MinTemperature);
            Assert.Null(model.Precipitation);
        }
    }

    [Fact]
    public void Window_is_limited_to_five_days()
    {
        var models = ImmutableDictionary.CreateRange([
            KeyValuePair.Create("model_a", ModelForecast(_now, 7 * 24, 20.0, 1.0)),
            KeyValuePair.Create("model_b", ModelForecast(_now, 7 * 24, 22.0, 2.0)),
        ]);

        var result = ModelComparisonHelper.CompareDays(models, _now);

        Assert.True(result.Length <= ModelComparisonHelper.ComparedDays);
        var today = DateOnly.FromDateTime(_now.UtcDateTime);
        foreach (var day in result)
        {
            Assert.True(day.Date >= today);
            Assert.True(day.Date < today.AddDays(ModelComparisonHelper.ComparedDays));
        }
    }
}
