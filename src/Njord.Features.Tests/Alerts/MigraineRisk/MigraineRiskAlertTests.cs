using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.MigraineRisk;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts.MigraineRisk;

public sealed class MigraineRiskAlertTests
{
    private static MigraineRiskAlert Feature() => new(Options.Create(new MigraineRiskConfig { Enabled = true }));

    // Hourly points from 24 h before now up to 2 h after, shaped by the hour offset relative to now.
    private static LocationForecast History(Func<int, ForecastPoint, ForecastPoint>? shape = null)
    {
        var points = Enumerable.Range(-24, 27)
            .Select(offset =>
            {
                var point = Benign(Now.AddHours(offset));
                return shape is null ? point : shape(offset, point);
            })
            .ToImmutableArray();
        return ForecastOnly(new DomainForecast<ForecastPoint>(DomainKind.Forecast, Now, TimeResolution.Hourly, points));
    }

    private static MigraineRiskAlertResult? Alert(LocationForecast forecast) =>
        Run(Feature(), Now, forecast) as MigraineRiskAlertResult;

    [Fact]
    public void Steady_conditions_raise_nothing()
    {
        Assert.Null(Run(Feature(), Now, History()));
    }

    [Theory]
    [InlineData(4.9, AlertSeverity.None)]
    [InlineData(5.0, AlertSeverity.Yellow)]
    [InlineData(9.0, AlertSeverity.Yellow)]
    public void A_pressure_drop_over_six_hours_raises_yellow_at_the_threshold(double drop, AlertSeverity expected)
    {
        var result = Alert(History((offset, p) => offset == -6 ? p with { Pressure = 1013.0 + drop } : p));

        if (expected == AlertSeverity.None)
        {
            Assert.Null(result);
            return;
        }

        var alert = Assert.IsType<MigraineRiskAlertResult>(result);
        Assert.Multiple(
            () => Assert.Equal(expected, alert.Severity),
            () => Assert.Equal(drop, alert.TriggerValue),
            () => Assert.Equal(5.0, alert.Threshold));
    }

    [Fact]
    public void A_pressure_drop_over_twenty_four_hours_raises_orange()
    {
        var alert = Assert.IsType<MigraineRiskAlertResult>(
            Alert(History((offset, p) => offset == -24 ? p with { Pressure = 1023.0 } : p)));

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(10.0, alert.TriggerValue),
            () => Assert.Equal(10.0, alert.Threshold));
    }

    [Fact]
    public void A_large_temperature_swing_raises_the_level_by_one()
    {
        var alert = Assert.IsType<MigraineRiskAlertResult>(Alert(History((offset, p) => offset switch
        {
            -6 => p with { Pressure = 1018.0 },
            -12 => p with { Temperature = 25.0 },
            _ => p,
        })));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Temperature_and_humidity_swings_together_raise_the_level_to_red()
    {
        var alert = Assert.IsType<MigraineRiskAlertResult>(Alert(History((offset, p) => offset switch
        {
            -6 => p with { Pressure = 1018.0 },
            -12 => p with { Temperature = 25.0, Humidity = 90.0 },
            _ => p,
        })));

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Swings_alone_raise_nothing_without_a_pressure_drop()
    {
        Assert.Null(Alert(History((offset, p) => offset == -12 ? p with { Temperature = 30.0, Humidity = 95.0 } : p)));
    }

    [Fact]
    public void Foehn_conditions_raise_orange_on_their_own()
    {
        var alert = Assert.IsType<MigraineRiskAlertResult>(Alert(History((offset, p) => offset switch
        {
            0 => p with { WindDirection = 180.0 },
            -3 => p with { Temperature = 10.0, Humidity = 70.0, Pressure = 1010.0 },
            _ => p,
        })));

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Foehn_conditions_from_another_wind_direction_raise_nothing()
    {
        Assert.Null(Alert(History((offset, p) => offset switch
        {
            0 => p with { WindDirection = 90.0 },
            -3 => p with { Temperature = 10.0, Humidity = 70.0, Pressure = 1010.0 },
            _ => p,
        })));
    }

    [Fact]
    public void Foehn_on_top_of_a_pressure_drop_raises_red()
    {
        var alert = Assert.IsType<MigraineRiskAlertResult>(Alert(History((offset, p) => offset switch
        {
            0 => p with { WindDirection = 180.0 },
            -3 => p with { Temperature = 10.0, Humidity = 70.0, Pressure = 1010.0 },
            -6 => p with { Pressure = 1020.0 },
            _ => p,
        })));

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Earlier_forecasts_in_the_ring_supply_the_lookback()
    {
        var earlier = History((offset, p) => offset == -6 ? p with { Pressure = 1020.0 } : p);
        var current = ForecastOnly(Hourly(3));

        var alert = Assert.IsType<MigraineRiskAlertResult>(Run(Feature(), Now, earlier, current));

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Missing_pressure_at_the_current_hour_raises_nothing()
    {
        Assert.Null(Alert(History((offset, p) => offset == 0 ? p with { Pressure = null } : p)));
    }
}
