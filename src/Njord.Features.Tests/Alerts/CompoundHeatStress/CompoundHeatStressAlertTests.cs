using Microsoft.Extensions.Options;
using Njord.Features.Alerts.CompoundHeatStress;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.CompoundHeatStress;

public sealed class CompoundHeatStressAlertTests
{
    private static CompoundHeatStressAlert Feature() => new(Options.Create(new CompoundHeatStressConfig { Enabled = true }));

    [Theory]
    [InlineData(27.9, AlertSeverity.None)]
    [InlineData(28.0, AlertSeverity.Yellow)]
    [InlineData(32.0, AlertSeverity.Orange)]
    [InlineData(35.0, AlertSeverity.Red)]
    public void Wet_bulb_escalates_through_the_configured_levels(double wetBulb, AlertSeverity expected)
    {
        var weather = Uniform(p => p with { WetBulbTemperature = wetBulb });

        var result = Run(Feature(), Now, Combined(weather, CleanAir()));

        if (expected == AlertSeverity.None)
        {
            Assert.Null(result);
            return;
        }

        var alert = Assert.IsType<CompoundHeatStressAlertResult>(result);
        Assert.Equal(expected, alert.Severity);
    }

    [Fact]
    public void Extreme_wet_bulb_reports_the_peak_hour_and_threshold()
    {
        var weather = Day((hour, p) => hour == 5 ? p with { WetBulbTemperature = 36.0 } : p with { WetBulbTemperature = 20.0 });

        var alert = Assert.IsType<CompoundHeatStressAlertResult>(Run(Feature(), Now, Combined(weather, CleanAir())));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, alert.Severity),
            () => Assert.Equal(36.0, alert.TriggerValue),
            () => Assert.Equal(35.0, alert.Threshold),
            () => Assert.Equal(5, alert.HoursUntil));
    }

    [Fact]
    public void Hot_apparent_temperature_with_fine_particles_is_yellow()
    {
        var weather = Uniform(p => p with { ApparentTemperature = 41.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 30.0 });

        var alert = Assert.IsType<CompoundHeatStressAlertResult>(Run(Feature(), Now, Combined(weather, air)));

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Very_hot_apparent_temperature_with_very_high_particles_is_orange()
    {
        var weather = Uniform(p => p with { ApparentTemperature = 46.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 60.0 });

        var alert = Assert.IsType<CompoundHeatStressAlertResult>(Run(Feature(), Now, Combined(weather, air)));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Hot_apparent_temperature_with_clean_air_raises_nothing()
    {
        var weather = Uniform(p => p with { ApparentTemperature = 45.0 });

        Assert.Null(Run(Feature(), Now, Combined(weather, CleanAir())));
    }

    [Fact]
    public void Strong_uv_under_a_shallow_boundary_layer_with_particles_is_yellow()
    {
        var weather = Uniform(p => p with { UvIndex = 9.0, BoundaryLayerHeight = 300.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 16.0 });

        var alert = Assert.IsType<CompoundHeatStressAlertResult>(Run(Feature(), Now, Combined(weather, air)));

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Strong_uv_with_a_deep_boundary_layer_raises_nothing()
    {
        var weather = Uniform(p => p with { UvIndex = 9.0, BoundaryLayerHeight = 1500.0 });
        var air = CleanAir(shape: (_, p) => p with { Pm25 = 16.0 });

        Assert.Null(Run(Feature(), Now, Combined(weather, air)));
    }

    [Fact]
    public void Safe_conditions_raise_nothing()
    {
        Assert.Null(Run(Feature(), Now, Combined(Day(), CleanAir())));
    }
}
