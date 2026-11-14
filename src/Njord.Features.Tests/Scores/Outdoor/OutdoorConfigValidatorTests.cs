using Njord.Features.Scores.Outdoor;

namespace Njord.Features.Tests.Scores.Outdoor;

public sealed class OutdoorConfigValidatorTests
{
    private readonly OutdoorConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new OutdoorConfig()).Succeeded);

    [Fact]
    public void Weights_not_summing_to_one_are_rejected()
    {
        var result = _validator.Validate(null, new OutdoorConfig { TemperatureWeight = 0.5 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("weights must sum to 1.0"));
    }

    [Fact]
    public void Inverted_temperature_band_is_rejected()
    {
        var result = _validator.Validate(null, new OutdoorConfig { TemperatureIdeal = 30.0, TemperatureWarm = 20.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("TemperatureIdeal"));
    }

    [Fact]
    public void Inverted_wind_band_is_rejected()
    {
        var result = _validator.Validate(null, new OutdoorConfig { WindIdeal = 10.0, WindModerate = 5.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WindIdeal"));
    }

    [Fact]
    public void Zero_good_hour_score_is_rejected()
    {
        var result = _validator.Validate(null, new OutdoorConfig { GoodHourScore = 0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("GoodHourScore"));
    }
}
