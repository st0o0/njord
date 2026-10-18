using Njord.Alerts.Configuration;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;

namespace Njord.Alerts.Tests.Configuration;

public sealed class AlertSettingsConversionSpec
{
    [Fact]
    public void Threshold_arrays_become_typed_severity_thresholds()
    {
        var settings = AppSettingsDefaults.Options<AlertOptions>(o => o.FrostThresholds = [1, -4, -12]).ToSettings();

        Assert.Equal(new SeverityThresholds(1, -4, -12, ScanDirection.Min), settings.Frost);
        Assert.Equal(new SeverityThresholds(30, 35, 40, ScanDirection.Max), settings.Heat);
        Assert.Equal(ScanDirection.Min, settings.WindChill.Direction);
        Assert.Equal(ScanDirection.Min, settings.Visibility.Direction);
        Assert.Equal(ScanDirection.Max, settings.StormGust.Direction);
        Assert.Equal(ScanDirection.Max, settings.TropicalNight.Direction);
        Assert.Equal(ScanDirection.Max, settings.Humidity.Direction);
    }

    [Fact]
    public void Scalar_thresholds_are_carried_over()
    {
        var options = new AlertOptions { CapeThreshold = 1500, FogPersistentHours = 6, HeavyRainDailyThreshold = 30 };

        var settings = options.ToSettings();

        Assert.Equal(1500, settings.CapeThreshold);
        Assert.Equal(6, settings.FogPersistentHours);
        Assert.Equal(30, settings.HeavyRainDailyThreshold);
        Assert.Equal(options.PressureDropSevereThreshold, settings.PressureDropSevereThreshold);
    }

    [Fact]
    public void Invalid_threshold_arrays_cannot_be_converted()
    {
        Assert.Throws<InvalidOperationException>(() => AppSettingsDefaults.Options<AlertOptions>(o => o.HeatThresholds = [30]).ToSettings());
    }
}
