using Njord.Core.Configuration;
using Njord.Model;

namespace Njord.Core.Tests.Configuration;

public sealed class ParameterSelectionSpec
{
    [Fact]
    public void The_weather_group_resolves_to_every_weather_parameter()
    {
        var resolved = ParameterSelection.Resolve(["Weather"], [], []);

        Assert.Multiple(
            () => Assert.Equal(31, resolved.Hourly.Count),
            () => Assert.Equal(17, resolved.Daily.Count),
            () => Assert.All(resolved.Hourly, p => Assert.Equal(ParameterGranularity.Hourly, p.Granularity)),
            () => Assert.All(resolved.Daily, p => Assert.Equal(ParameterGranularity.Daily, p.Granularity)),
            () => Assert.All([.. resolved.Hourly, .. resolved.Daily], p => Assert.Equal(ParameterGroup.Weather, p.Group)));
    }

    [Fact]
    public void Group_names_are_case_insensitive()
    {
        var resolved = ParameterSelection.Resolve(["weather", "SOLAR"], [], []);

        var groups = resolved.Hourly.Concat(resolved.Daily).Select(p => p.Group).Distinct().Order().ToList();

        Assert.Equal([ParameterGroup.Weather, ParameterGroup.Solar], groups);
    }

    [Fact]
    public void Every_group_member_is_accepted_by_name()
    {
        foreach (var group in Enum.GetValues<ParameterGroup>())
        {
            var resolved = ParameterSelection.Resolve([group.ToString()], [], []);

            Assert.All(resolved.Hourly.Concat(resolved.Daily), p => Assert.Equal(group, p.Group));
        }
    }

    [Fact]
    public void Extra_adds_parameters_from_other_groups()
    {
        var resolved = ParameterSelection.Resolve(["Weather"], ["uv_index"], []);

        Assert.Contains(ParameterRegistry.UvIndex, resolved.Hourly);
    }

    [Fact]
    public void Extra_by_api_name_adds_the_hourly_and_the_daily_variant_is_not_implied()
    {
        var resolved = ParameterSelection.Resolve(["Weather"], ["sunshine_duration"], []);

        Assert.Contains(ParameterRegistry.SunshineDuration, resolved.Hourly);
        Assert.DoesNotContain(ParameterRegistry.DailySunshineDuration, resolved.Daily);
    }

    [Fact]
    public void Exclude_removes_parameters()
    {
        var resolved = ParameterSelection.Resolve(["Weather"], [], ["cape"]);

        Assert.DoesNotContain(resolved.Hourly, p => p.ApiName == "cape");
    }

    [Fact]
    public void An_unknown_group_is_reported_with_the_valid_groups()
    {
        var ex = Assert.Throws<ParameterResolutionException>(() => ParameterSelection.Resolve(["Marine"], [], []));

        var error = Assert.Single(ex.Errors);
        Assert.Equal("Unknown parameter group: 'Marine'. Valid groups: Weather, Solar, Soil", error);
    }

    [Fact]
    public void An_unknown_extra_is_reported()
    {
        var ex = Assert.Throws<ParameterResolutionException>(() => ParameterSelection.Resolve(["Weather"], ["not_real"], []));

        Assert.Equal("Unknown parameter in Extra: 'not_real'", Assert.Single(ex.Errors));
    }

    [Fact]
    public void An_unknown_exclude_is_reported()
    {
        var ex = Assert.Throws<ParameterResolutionException>(() => ParameterSelection.Resolve(["Weather"], [], ["not_real"]));

        Assert.Equal("Unknown parameter in Exclude: 'not_real'", Assert.Single(ex.Errors));
    }

    [Fact]
    public void An_empty_result_is_reported()
    {
        var everything = ParameterRegistry.GetByGroup(ParameterGroup.Weather).Select(p => p.ApiName).Distinct().ToList();

        var ex = Assert.Throws<ParameterResolutionException>(() => ParameterSelection.Resolve(["Weather"], [], everything));

        Assert.Equal(
            "The resolved parameter set is empty. Enable at least one group or add parameters via Extra.",
            Assert.Single(ex.Errors));
    }

    [Fact]
    public void All_errors_are_collected_into_one_exception()
    {
        var ex = Assert.Throws<ParameterResolutionException>(
            () => ParameterSelection.Resolve(["Marine"], ["not_real"], ["also_not_real"]));

        Assert.Multiple(
            () => Assert.Equal(3, ex.Errors.Count),
            () => Assert.Equal(string.Join("; ", ex.Errors), ex.Message),
            () => Assert.Contains("Unknown parameter group: 'Marine'", ex.Errors[0]),
            () => Assert.Equal("Unknown parameter in Extra: 'not_real'", ex.Errors[1]),
            () => Assert.Equal("Unknown parameter in Exclude: 'also_not_real'", ex.Errors[2]));
    }
}
