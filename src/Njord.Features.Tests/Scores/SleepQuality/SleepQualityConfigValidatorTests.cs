using Njord.Features.Scores.SleepQuality;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Scores.SleepQuality;

public sealed class SleepQualityConfigValidatorTests
{
    private readonly SleepQualityConfigValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new SleepQualityConfig()).Succeeded);
    }

    [Fact]
    public void A_night_start_hour_above_23_is_rejected()
    {
        var result = _validator.Validate(null, new SleepQualityConfig { NightStartHour = 24 });

        Assert.Contains("NightStartHour must be 0..23", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void A_negative_night_end_hour_is_rejected()
    {
        var result = _validator.Validate(null, new SleepQualityConfig { NightEndHour = -1 });

        Assert.Contains("NightEndHour must be 0..23", ValidationAssert.SingleFailure(result));
    }

    [Theory]
    [InlineData(nameof(SleepQualityConfig.MinWindowScore))]
    [InlineData(nameof(SleepQualityConfig.MinWindowHours))]
    [InlineData(nameof(SleepQualityConfig.MaxWindows))]
    public void A_window_param_below_one_is_rejected(string property)
    {
        var config = new SleepQualityConfig();
        typeof(SleepQualityConfig).GetProperty(property)!.SetValue(config, 0);

        var result = _validator.Validate(null, config);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains($"{property} must be at least 1"));
    }
}
