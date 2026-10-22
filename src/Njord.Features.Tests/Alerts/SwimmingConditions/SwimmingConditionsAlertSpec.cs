using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Alerts.SwimmingConditions;
using Njord.Forecast.Marine;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.FloodFixtures;
using M = Njord.Tests.Shared.MarineFixtures;

namespace Njord.Features.Tests.Alerts;

public sealed class SwimmingConditionsAlertSpec
{
    private static SwimmingConditionsAlert Feature() => new(Options.Create(new SwimmingConditionsConfig { Enabled = true }));

    private static PipelineEvent? RunWith(params MarinePoint[] points) =>
        Run(Feature(), Now, new LocationForecast(Lucerne, Rain(0.0), Marine: M.MarineForecast(points)));

    [Theory]
    [InlineData(0.3, 20.0, AlertSeverity.None)]
    [InlineData(0.99, 16.0, AlertSeverity.None)]
    [InlineData(1.0, 20.0, AlertSeverity.Yellow)]
    [InlineData(0.3, 15.9, AlertSeverity.Yellow)]
    [InlineData(1.9, 20.0, AlertSeverity.Yellow)]
    [InlineData(2.0, 20.0, AlertSeverity.Red)]
    [InlineData(0.3, 9.9, AlertSeverity.Red)]
    [InlineData(1.2, 9.0, AlertSeverity.Red)]
    public void Severity_follows_wave_height_and_water_temperature(double wave, double temperature, AlertSeverity expected)
    {
        var events = RunWith(M.Point(0, wave, temperature));

        if (expected == AlertSeverity.None)
        {
            Assert.Null(events);
            return;
        }

        Assert.Equal(expected, Assert.IsType<SwimmingConditionsAlertResult>(events).Severity);
    }

    [Fact]
    public void A_missing_water_temperature_is_judged_by_wave_height_alone()
    {
        Assert.Multiple(
            () => Assert.Null(RunWith(M.Point(0, 0.3, null))),
            () => Assert.Equal(
                AlertSeverity.Yellow,
                Assert.IsType<SwimmingConditionsAlertResult>(RunWith(M.Point(0, 1.5, null))).Severity));
    }

    [Fact]
    public void The_worst_hour_is_reported_with_its_offset()
    {
        var alert = Assert.IsType<SwimmingConditionsAlertResult>(RunWith(M.Point(6, 2.6)));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(2.6, alert.TriggerValue),
            () => Assert.Equal(2.0, alert.Threshold),
            () => Assert.Equal(6, alert.HoursUntil));
    }

    [Fact]
    public void Hours_beyond_the_24_hour_window_are_ignored()
    {
        Assert.Null(RunWith(M.Point(30, 2.6)));
    }

    [Fact]
    public void Marine_data_without_weather_raises_nothing()
    {
        Assert.Null(Run(Feature(), Now, new LocationForecast(Lucerne, Marine: M.MarineForecast(M.Point(0, 2.6)))));
    }

    [Fact]
    public void Wave_height_is_required_and_water_temperature_is_optional()
    {
        Assert.Equal(
            new DomainRequirements(Marine: MarineParams.WaveHeight, OptionalMarine: MarineParams.WaterTemperature),
            Feature().Descriptor.Requirements);
    }
}
