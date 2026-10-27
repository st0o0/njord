using Njord.Features.Alerts.Dust;
using Njord.Features.Alerts.Frost;
using Njord.Features.Alerts.Heat;
using Njord.Features.Alerts.Humidity;
using Njord.Features.Alerts.Storm;
using Njord.Features.Alerts.TropicalNight;
using Njord.Features.Alerts.Visibility;
using Njord.Features.Alerts.WindChill;
using Njord.Tests.Shared;

namespace Njord.Features.Tests.Alerts;

public sealed class AlertConfigValidatorsTests
{
    [Fact]
    public void Should_accept_unset_thresholds_for_every_alert()
    {
        Assert.Multiple(
            () => Assert.True(new DustConfigValidator().Validate(null, new DustConfig()).Succeeded),
            () => Assert.True(new FrostConfigValidator().Validate(null, new FrostConfig()).Succeeded),
            () => Assert.True(new HeatConfigValidator().Validate(null, new HeatConfig()).Succeeded),
            () => Assert.True(new HumidityConfigValidator().Validate(null, new HumidityConfig()).Succeeded),
            () => Assert.True(new StormConfigValidator().Validate(null, new StormConfig()).Succeeded),
            () => Assert.True(new TropicalNightConfigValidator().Validate(null, new TropicalNightConfig()).Succeeded),
            () => Assert.True(new VisibilityConfigValidator().Validate(null, new VisibilityConfig()).Succeeded),
            () => Assert.True(new WindChillConfigValidator().Validate(null, new WindChillConfig()).Succeeded));
    }

    [Fact]
    public void Should_accept_ascending_thresholds_for_a_max_scan_alert()
    {
        var result = new HeatConfigValidator().Validate(null, new HeatConfig { Thresholds = [28, 33, 38] });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Should_accept_descending_thresholds_for_a_min_scan_alert()
    {
        var result = new FrostConfigValidator().Validate(null, new FrostConfig { Thresholds = [1, -4, -10] });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Should_reject_fewer_than_three_thresholds()
    {
        var result = new HeatConfigValidator().Validate(null, new HeatConfig { Thresholds = [30, 35] });

        Assert.Contains("Njord:Features:Forecast:Heat:Thresholds needs three values", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_descending_thresholds_for_a_max_scan_alert()
    {
        var result = new HeatConfigValidator().Validate(null, new HeatConfig { Thresholds = [40, 35, 30] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_ascending_thresholds_for_a_min_scan_alert()
    {
        var result = new FrostConfigValidator().Validate(null, new FrostConfig { Thresholds = [-15, -5, 0] });

        Assert.Contains("must be strictly descending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_equal_thresholds()
    {
        var result = new DustConfigValidator().Validate(null, new DustConfig { Thresholds = [50, 50, 500] });

        Assert.Contains("must be strictly ascending", ValidationAssert.SingleFailure(result));
    }

    [Fact]
    public void Should_reject_misordered_thresholds_for_every_remaining_alert()
    {
        Assert.Multiple(
            () => Assert.False(new HumidityConfigValidator().Validate(null, new HumidityConfig { Thresholds = [24, 21, 16] }).Succeeded),
            () => Assert.False(new StormConfigValidator().Validate(null, new StormConfig { GustThresholds = [33, 25, 17] }).Succeeded),
            () => Assert.False(new TropicalNightConfigValidator().Validate(null, new TropicalNightConfig { Thresholds = [25, 23, 20] }).Succeeded),
            () => Assert.False(new VisibilityConfigValidator().Validate(null, new VisibilityConfig { Thresholds = [50, 200, 1000] }).Succeeded),
            () => Assert.False(new WindChillConfigValidator().Validate(null, new WindChillConfig { Thresholds = [-30, -20, -10] }).Succeeded));
    }
}
