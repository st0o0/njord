using Njord.Core.Configuration;

namespace Njord.Core.Tests.Configuration;

public sealed class ParameterSelectionSpec
{
    [Fact]
    public void Should_resolve_the_weather_group_case_insensitively()
    {
        var set = ParameterSelection.Resolve(["weather"], [], []);

        Assert.Multiple(
            () => Assert.Contains(ParameterRegistry.Temperature2m, set.Hourly),
            () => Assert.All(set.Hourly, p => Assert.Equal(ParameterGranularity.Hourly, p.Granularity)),
            () => Assert.All(set.Daily, p => Assert.Equal(ParameterGranularity.Daily, p.Granularity)));
    }

    [Fact]
    public void Should_remove_excluded_parameters()
    {
        var set = ParameterSelection.Resolve(["Weather"], [], ["temperature_2m"]);

        Assert.DoesNotContain(ParameterRegistry.Temperature2m, set.Hourly);
    }

    [Fact]
    public void Should_add_extra_parameters_outside_the_selected_groups()
    {
        var baseline = ParameterSelection.Resolve(["Weather"], [], []);
        var withExtra = ParameterSelection.Resolve(["Weather"], ["soil_temperature_0cm"], []);

        Assert.Equal(baseline.HourlyCount + 1, withExtra.HourlyCount);
    }

    [Fact]
    public void Should_report_every_unknown_name_in_one_exception()
    {
        var ex = Assert.Throws<ParameterResolutionException>(
            () => ParameterSelection.Resolve(["Lava"], ["nope"], ["nada"]));

        Assert.Equal(
            [
                "Unknown parameter group: 'Lava'. Valid groups: Weather, Solar, Soil",
                "Unknown parameter in Extra: 'nope'",
                "Unknown parameter in Exclude: 'nada'",
            ],
            ex.Errors);
    }

    [Fact]
    public void Should_reject_an_empty_resolved_set()
    {
        var ex = Assert.Throws<ParameterResolutionException>(() => ParameterSelection.Resolve([], [], []));

        Assert.Contains("empty", Assert.Single(ex.Errors));
    }

    [Fact]
    public void Should_weigh_api_calls_by_tens_of_hourly_parameters()
    {
        var set = ParameterSelection.Resolve(["Weather"], [], []);

        Assert.Equal((int)Math.Ceiling(set.HourlyCount / 10.0), set.ApiCallWeight);
    }
}
