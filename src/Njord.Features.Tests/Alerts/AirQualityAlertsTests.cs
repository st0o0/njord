using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Features.Alerts.Dust;
using Njord.Features.Alerts.Pm25;
using Njord.Features.Alerts.Pollen;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using static Njord.Tests.Shared.AirQualityFixture;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Alerts;

public sealed class AirQualityAlertsTests
{
    private static PollenAlert Pollen() => new(Options.Create(new PollenConfig()));

    private static Pm25Alert Pm25() => new(Options.Create(new Pm25Config()));

    private static DustAlert Dust() => new(Options.Create(new DustConfig { Enabled = true, Thresholds = DustConfig.DefaultThresholds }));

    private static LocationForecast With(Func<int, AirQualityPoint, AirQualityPoint>? shape = null) =>
        new(Lucerne, AirQuality: Day(shape));

    [Fact]
    public void Pollen_at_the_first_level_is_yellow()
    {
        var alert = Assert.IsType<PollenAlertResult>(Run(Pollen(), Now, With((_, p) => p with { BirchPollen = 20.0 })));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(20.0, alert.Threshold));
    }

    [Fact]
    public void Pollen_below_the_first_level_raises_nothing()
    {
        Assert.Null(Run(Pollen(), Now, With((_, p) => p with { BirchPollen = 19.9 })));
    }

    [Fact]
    public void The_strongest_pollen_in_the_window_decides()
    {
        var forecast = With((h, p) => h switch
        {
            3 => p with { BirchPollen = 30.0 },
            9 => p with { GrassPollen = 120.0 },
            _ => p,
        });

        var alert = Assert.IsType<PollenAlertResult>(Run(Pollen(), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(120.0, alert.TriggerValue),
            () => Assert.Equal(9, alert.HoursUntil));
    }

    [Fact]
    public void Fine_particles_at_the_who_guideline_are_yellow()
    {
        var alert = Assert.IsType<Pm25AlertResult>(Run(Pm25(), Now, With((_, p) => p with { Pm25 = 15.0 })));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(15.0, alert.Threshold));
    }

    [Fact]
    public void Fine_particles_at_25_are_orange()
    {
        var alert = Assert.IsType<Pm25AlertResult>(Run(Pm25(), Now, With((_, p) => p with { Pm25 = 25.0 })));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void The_pm25_peak_is_reported_with_its_hour()
    {
        var alert = Assert.IsType<Pm25AlertResult>(Run(Pm25(), Now, With((h, p) => h == 5 ? p with { Pm25 = 31.04 } : p)));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(31.0, alert.TriggerValue),
            () => Assert.Equal(25.0, alert.Threshold),
            () => Assert.Equal(5, alert.HoursUntil));
    }

    [Theory]
    [InlineData(49.9, AlertSeverity.None)]
    [InlineData(50.0, AlertSeverity.Yellow)]
    [InlineData(150.0, AlertSeverity.Orange)]
    [InlineData(500.0, AlertSeverity.Red)]
    public void Dust_severity_follows_the_configured_thresholds(double dust, AlertSeverity expected)
    {
        var events = Run(Dust(), Now, With((h, p) => h == 4 ? p with { Dust = dust } : p));

        if (expected == AlertSeverity.None)
        {
            Assert.Null(events);
            return;
        }

        var alert = Assert.IsType<DustAlertResult>(events);
        Assert.Multiple(
            () => Assert.Equal(expected, alert.Severity),
            () => Assert.Equal(4, alert.HoursUntil));
    }

    [Fact]
    public void Clean_air_raises_no_air_quality_alert()
    {
        var forecast = With();

        Assert.Multiple(
            () => Assert.Null(Run(Pollen(), Now, forecast)),
            () => Assert.Null(Run(Pm25(), Now, forecast)),
            () => Assert.Null(Run(Dust(), Now, forecast)));
    }

    [Fact]
    public void Missing_air_quality_data_raises_nothing()
    {
        Assert.Null(Run(Pm25(), Now, new LocationForecast(Lucerne)));
    }

    [Fact]
    public void Each_feature_requests_its_own_air_quality_parameters()
    {
        Assert.Multiple(
            () => Assert.Equal(
                new DomainRequirements(AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                    | AirQualityParams.Alder | AirQualityParams.Mugwort),
                Pollen().Descriptor.Requirements),
            () => Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Pm25), Pm25().Descriptor.Requirements),
            () => Assert.Equal(new DomainRequirements(AirQuality: AirQualityParams.Dust), Dust().Descriptor.Requirements));
    }
}
