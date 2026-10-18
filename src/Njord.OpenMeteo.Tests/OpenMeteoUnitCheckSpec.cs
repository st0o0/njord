using Njord.Core.Configuration;
using Njord.OpenMeteo.Mapping;
using Njord.Weather;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoUnitCheckSpec
{
    private static readonly ResolvedParameterSet Parameters = ParameterSelection.Resolve(["Weather"], [], []);

    [Fact]
    public void Missing_units_are_reported()
    {
        Assert.Equal("Response carried no hourly_units", OpenMeteoUnitCheck.Mismatch(null, Parameters));
        Assert.Equal("Response carried no hourly_units", OpenMeteoUnitCheck.Mismatch([], Parameters));
    }

    [Fact]
    public void A_time_unit_other_than_unixtime_is_reported()
    {
        var mismatch = OpenMeteoUnitCheck.Mismatch(new() { ["time"] = "iso8601" }, Parameters);

        Assert.Equal("Unexpected unit 'iso8601' for time (expected 'unixtime')", mismatch);
    }

    [Fact]
    public void A_wind_speed_in_kmh_is_reported()
    {
        var mismatch = OpenMeteoUnitCheck.Mismatch(
            new() { ["time"] = "unixtime", ["wind_speed_10m"] = "km/h" }, Parameters);

        Assert.Equal("Unexpected unit 'km/h' for wind_speed_10m (expected 'm/s')", mismatch);
    }

    [Fact]
    public void Matching_and_undefined_units_pass()
    {
        var units = new Dictionary<string, string>
        {
            ["time"] = "unixtime",
            ["temperature_2m"] = "°C",
            ["wind_speed_10m"] = "undefined",
            ["pressure_msl"] = "hPa",
        };

        Assert.Null(OpenMeteoUnitCheck.Mismatch(units, Parameters));
    }

    private static readonly string[] CheckedHourlyParameters =
    [
        "temperature_2m",
        "apparent_temperature",
        "dew_point_2m",
        "soil_temperature_0cm",
        "soil_temperature_6cm",
        "soil_temperature_18cm",
        "soil_temperature_54cm",
        "wind_speed_10m",
        "wind_speed_80m",
        "wind_speed_120m",
        "wind_speed_180m",
        "wind_gusts_10m",
        "pressure_msl",
        "surface_pressure",
    ];

    [Fact]
    public void Exactly_the_temperature_wind_speed_and_pressure_parameters_are_checked()
    {
        var all = new ResolvedParameterSet(
            [.. ParameterRegistry.All.Where(p => p.Granularity == ParameterGranularity.Hourly)],
            []);

        var checkedNames = all.Hourly
            .Where(p => OpenMeteoUnitCheck.Mismatch(
                new() { ["time"] = "unixtime", [p.ApiName] = "bogus" },
                new ResolvedParameterSet([p], [])) is not null)
            .Select(p => p.ApiName)
            .Order()
            .ToList();

        Assert.Equal(CheckedHourlyParameters.Order().ToList(), checkedNames);
    }

    [Fact]
    public void Parameters_absent_from_the_units_are_not_checked()
    {
        Assert.Null(OpenMeteoUnitCheck.Mismatch(new() { ["time"] = "unixtime" }, Parameters));
    }
}
