using Njord.Core.Configuration;
using Njord.Model;

namespace Njord.Core.Tests.Configuration;

public sealed class NjordOptionsValidatorSpec
{
    private readonly NjordOptionsValidator _validator = new();

    private static NjordOptions Valid() => new()
    {
        Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }],
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

        Assert.Equal("At least one location (Name, Latitude and Longitude) is required.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_location_with_a_blank_name()
    {
        var options = Valid();
        options.Locations = [new LocationOptions { Name = " ", Latitude = 47.05, Longitude = 8.31 }];

        var result = _validator.Validate(null, options);

        Assert.Equal("Every location needs a non-empty Name.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_location_without_a_latitude()
    {
        var options = Valid();
        options.Locations = [new LocationOptions { Name = "Lucerne", Longitude = 8.31 }];

        var result = _validator.Validate(null, options);

        Assert.Equal("Location 'Lucerne' needs a Latitude.", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_a_location_without_a_longitude()
    {
        var options = Valid();
        options.Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05 }];

        var result = _validator.Validate(null, options);

        Assert.Equal("Location 'Lucerne' needs a Longitude.", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(90.5, 8.31, "Location 'Lucerne' has a Latitude outside -90..90.")]
    [InlineData(-91, 8.31, "Location 'Lucerne' has a Latitude outside -90..90.")]
    [InlineData(47.05, 180.5, "Location 'Lucerne' has a Longitude outside -180..180.")]
    [InlineData(47.05, -181, "Location 'Lucerne' has a Longitude outside -180..180.")]
    public void Should_reject_coordinates_out_of_range(double latitude, double longitude, string expected)
    {
        var options = Valid();
        options.Locations = [new LocationOptions { Name = "Lucerne", Latitude = latitude, Longitude = longitude }];

        var result = _validator.Validate(null, options);

        Assert.Equal(expected, ValidationAssert.SingleFailure(result));
    }
}
