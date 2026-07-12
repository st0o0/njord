using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Njord.Configuration;
using Njord.Domain;
using Njord.Pipeline;

namespace Njord.Tests.Pipeline;

public sealed class ExpandStageSpec
{
    private readonly ILogger _logger = NullLogger.Instance;

    private static NjordOptions Options(int locations = 2, params string[] models) => new()
    {
        Locations = [.. Enumerable.Range(1, locations).Select(i => new LocationOptions
        {
            Name = $"loc-{i}",
            Latitude = 47.0 + i,
            Longitude = 8.0 + i,
        })],
        Models = [.. models],
    };

    private static ResolvedParameterSet Parameters() =>
        ParameterRegistry.Resolve(["Weather"], [], []);

    [Fact(Timeout = 5000)]
    public void RefreshLocation_expands_to_all_models_for_that_location()
    {
        var options = Options(2, "icon_d2", "ecmwf_ifs025");
        var targets = ExpandStage.Expand(
            new PipelineCommand.RefreshLocation("loc-1"), options, Parameters(), _logger).ToList();

        Assert.Equal(2, targets.Count);
        Assert.All(targets, t => Assert.Equal("loc-1", t.Location.Name));
    }

    [Fact(Timeout = 5000)]
    public void RefreshModel_expands_to_single_target()
    {
        var options = Options(2, "icon_d2", "ecmwf_ifs025");
        var targets = ExpandStage.Expand(
            new PipelineCommand.RefreshModel("loc-1", new WeatherModel("icon_d2")),
            options, Parameters(), _logger).ToList();

        var target = Assert.Single(targets);
        Assert.Equal("loc-1", target.Location.Name);
        Assert.Equal("icon_d2", target.Model.Id);
    }

    [Fact(Timeout = 5000)]
    public void Unknown_location_emits_zero_targets()
    {
        var options = Options(1, "icon_d2");
        var targets = ExpandStage.Expand(
            new PipelineCommand.RefreshLocation("atlantis"), options, Parameters(), _logger).ToList();

        Assert.Empty(targets);
    }

    [Fact(Timeout = 5000)]
    public void Unknown_model_emits_zero_targets()
    {
        var options = Options(1, "icon_d2");
        var targets = ExpandStage.Expand(
            new PipelineCommand.RefreshModel("loc-1", new WeatherModel("nonexistent")),
            options, Parameters(), _logger).ToList();

        Assert.Empty(targets);
    }

    [Fact(Timeout = 5000)]
    public void Weight_matches_computed_value_from_config()
    {
        var options = Options(1, "icon_d2");
        options.ForecastDays = 4;
        var parameters = Parameters();
        var expectedWeight = WeightedTarget.ComputeWeight(parameters.HourlyCount, options.ForecastDays);
        var targets = ExpandStage.Expand(
            new PipelineCommand.RefreshModel("loc-1", new WeatherModel("icon_d2")),
            options, parameters, _logger).ToList();

        var target = Assert.Single(targets);
        Assert.Equal(expectedWeight, target.Weight);
    }

    [Fact(Timeout = 5000)]
    public void Extended_forecast_days_increase_weight()
    {
        Assert.Equal(2, WeightedTarget.ComputeWeight(9, 16));
    }

    [Fact(Timeout = 5000)]
    public void Extended_hourly_variables_increase_weight()
    {
        Assert.Equal(2, WeightedTarget.ComputeWeight(15, 4));
    }
}
