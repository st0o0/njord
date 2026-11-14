using Njord.Features.Scores.Bbq;

namespace Njord.Features.Tests.Scores.Bbq;

public sealed class BbqConfigValidatorTests
{
    private readonly BbqConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid() => Assert.True(_validator.Validate(null, new BbqConfig()).Succeeded);

    [Fact]
    public void Weights_not_summing_to_one_are_rejected()
    {
        var result = _validator.Validate(null, new BbqConfig { TemperatureWeight = 0.5 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("weights must sum to 1.0"));
    }

    [Fact]
    public void Valid_custom_weights_are_accepted()
    {
        var result = _validator.Validate(null, new BbqConfig
        {
            TemperatureWeight = 0.4,
            WindWeight = 0.2,
            RainWeight = 0.25,
            CloudWeight = 0.15,
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Inverted_temperature_band_is_rejected()
    {
        var result = _validator.Validate(null, new BbqConfig { TemperatureIdeal = 30.0, TemperatureWarm = 20.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("TemperatureIdeal"));
    }

    [Fact]
    public void Inverted_wind_band_is_rejected()
    {
        var result = _validator.Validate(null, new BbqConfig { WindModerate = 15.0, WindStrong = 5.0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("WindModerate"));
    }

    [Fact]
    public void Zero_good_hour_score_is_rejected()
    {
        var result = _validator.Validate(null, new BbqConfig { GoodHourScore = 0 });

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("GoodHourScore"));
    }
}
