using Njord.Core.Configuration;
using Njord.Model;

namespace Njord.Core.Tests.Configuration;

public sealed class NjordOptionsValidatorSpec
{
    private readonly NjordOptionsValidator _validator = new();

    private static NjordOptions Valid() => new()
    {
        Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }],
        Models = ["icon_d2"],
    };

    [Fact]
    public void Should_accept_a_minimal_valid_scope()
    {
        Assert.True(_validator.Validate(null, Valid()).Succeeded);
    }

    [Fact]
    public void Should_reject_a_scope_without_locations()
    {
        var options = Valid();
        options.Locations = [];

        var result = _validator.Validate(null, options);

        Assert.Equal("At least one location (Name; Latitude and Longitude optional, resolved by geocoding) is required.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_location_with_a_blank_name()
    {
        var options = Valid();
        options.Locations = [new LocationOptions { Name = " " }];

        var result = _validator.Validate(null, options);

        Assert.Equal("Every location needs a non-empty Name.", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Should_reject_forecast_days_outside_one_to_sixteen(int days)
    {
        var options = Valid();
        options.ForecastDays = days;

        var result = _validator.Validate(null, options);

        Assert.Equal("ForecastDays must be between 1 and 16.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_enabled_domains_without_weather()
    {
        var options = Valid();
        options.EnabledDomains = [DomainKind.Flood];

        var result = _validator.Validate(null, options);

        Assert.Equal("EnabledDomains must include Weather.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_duplicate_enabled_domains()
    {
        var options = Valid();
        options.EnabledDomains = [DomainKind.Weather, DomainKind.Weather];

        var result = _validator.Validate(null, options);

        Assert.Equal("EnabledDomains must not contain duplicates.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_report_unknown_parameter_groups()
    {
        var options = Valid();
        options.Parameters = new ParameterOptions { Groups = ["Weather", "Lava"] };

        var result = _validator.Validate(null, options);

        Assert.Contains("Unknown parameter group: 'Lava'", ValidationAssert.SingleFailure(result));
    }
}
