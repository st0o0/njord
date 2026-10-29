using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Solar;
using Njord.Features.Conditions.SolarYield;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.SolarYield;

public sealed class SolarYieldTests
{
    private const double Efficiency = 0.8;
    private const double PeakKw = 5.0;

    private static Features.Conditions.SolarYield.SolarYield Feature(double batteryCapacityKwh = 0) =>
        new(Options.Create(new SolarYieldConfig { Enabled = true, PeakKw = PeakKw, SystemEfficiency = Efficiency, BatteryCapacityKwh = batteryCapacityKwh }));

    private static LocationForecast Sunny() => ForecastOnly(Day((hour, point) => point with
    {
        GlobalTiltedIrradiance = hour == 3 ? 800.0 : 100.0,
    }));

    private static LocationForecast WithSatellite(params (int HoursAgo, double Gti)[] hours) =>
        Sunny() with
        {
            Satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Now, TimeResolution.Hourly,
                [.. hours.OrderByDescending(h => h.HoursAgo).Select(h => new SatelliteRadiationPoint(Now.AddHours(-h.HoursAgo), GTI: h.Gti))]),
        };

    [Fact]
    public void Tilted_irradiance_is_summed_and_scaled_by_efficiency()
    {
        var yield = Assert.IsType<SolarYieldResult>(Run(Feature(), Now, Sunny()));

        Assert.Multiple(
            () => Assert.Equal(Math.Round((23 * 100.0 + 800.0) / 1000.0 * Efficiency * PeakKw, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void Direct_and_diffuse_radiation_without_tilted_irradiance_yield_nothing()
    {
        var forecast = ForecastOnly(Day((_, point) => point with { DirectRadiation = 500.0, DiffuseRadiation = 50.0 }));

        Assert.Null(Run(Feature(), Now, forecast));
    }

    [Fact]
    public void Satellite_irradiance_is_used_up_to_the_newest_observation_and_the_ensemble_afterwards()
    {
        var yield = Assert.IsType<SolarYieldResult>(Run(Feature(), Now, WithSatellite((1, 50.0), (0, 400.0))));

        Assert.Multiple(
            () => Assert.Equal(Math.Round((400.0 + 800.0 + 22 * 100.0) / 1000.0 * Efficiency * PeakKw, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void A_measured_peak_wins_the_peak_hour()
    {
        var yield = Assert.IsType<SolarYieldResult>(Run(Feature(), Now, WithSatellite((0, 1000.0))));

        Assert.Equal(Now.Hour, yield.PeakHour);
    }

    [Fact]
    public void Satellite_data_older_than_three_hours_is_ignored()
    {
        var withStale = Assert.IsType<SolarYieldResult>(Run(Feature(), Now, WithSatellite((4, 900.0))));
        var ensembleOnly = Assert.IsType<SolarYieldResult>(Run(Feature(), Now, Sunny()));

        Assert.Equal(ensembleOnly, withStale);
    }

    [Fact]
    public void The_requirements_ask_for_global_tilted_irradiance()
    {
        Assert.Equal(ForecastParams.GlobalTiltedIrradiance, Feature().Descriptor.Requirements.Forecast);
    }

    [Fact]
    public void Nothing_is_reported_without_radiation_data()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Day())));
    }

    [Fact]
    public void The_requirements_ask_for_tilted_satellite_irradiance_without_gating_on_it()
    {
        Assert.Equal(SatelliteParams.GlobalTilted, Feature().Descriptor.Requirements.OptionalSatellite);
        Assert.Equal(SatelliteParams.None, Feature().Descriptor.Requirements.Satellite);
    }

    // 13 satellite hours today (00:00 to 12:00) and 24 forecast hours tomorrow, both as irradiance on the panel.
    private const double KwhPerWatt = PeakKw / 1000.0 * Efficiency;

    private static LocationForecast BatteryScenario(double todayGti, double tomorrowGti)
    {
        var forecast = Hourly(36, (_, point) => point with { GlobalTiltedIrradiance = tomorrowGti });
        var satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Now, TimeResolution.Hourly,
            [.. Enumerable.Range(0, 13).Select(hour => new SatelliteRadiationPoint(Now.AddHours(hour - 12), GTI: todayGti))]);
        return new(Lucerne, forecast, Satellite: satellite);
    }

    private static SolarBatteryStrategyConditionResult Battery(double todayGti, double tomorrowGti)
    {
        var events = RunAll(Feature(10.0), Now, BatteryScenario(todayGti, tomorrowGti));

        Assert.Equal(2, events.Count);
        Assert.IsType<SolarYieldResult>(events[0]);
        return Assert.IsType<SolarBatteryStrategyConditionResult>(events[1]);
    }

    [Fact]
    public void Without_a_battery_only_the_yield_is_emitted()
    {
        var events = RunAll(Feature(), Now, BatteryScenario(800.0, 1600.0));

        Assert.IsType<SolarYieldResult>(Assert.Single(events));
    }

    [Fact]
    public void A_sunny_tomorrow_after_a_surplus_today_feeds_in()
    {
        var result = Battery(800.0, 1600.0);

        Assert.Multiple(
            () => Assert.Equal(BatteryStrategy.FeedIn, result.Strategy),
            () => Assert.Equal(Math.Round(13 * 800.0 * KwhPerWatt, 2), result.TodayKwh),
            () => Assert.Equal(Math.Round(24 * 1600.0 * KwhPerWatt, 2), result.TomorrowKwhEstimate));
    }

    [Fact]
    public void A_cloudy_tomorrow_after_a_surplus_today_charges_the_battery()
    {
        Assert.Equal(BatteryStrategy.ChargeBattery, Battery(800.0, 100.0).Strategy);
    }

    [Fact]
    public void A_sunny_tomorrow_after_a_low_today_conserves()
    {
        Assert.Equal(BatteryStrategy.Conserve, Battery(50.0, 800.0).Strategy);
    }

    [Fact]
    public void Similar_days_are_normal()
    {
        Assert.Equal(BatteryStrategy.Normal, Battery(800.0, 800.0 * 13 / 24).Strategy);
    }

    [Fact]
    public void Tomorrow_uses_tilted_irradiance_not_horizontal_radiation()
    {
        var scenario = BatteryScenario(800.0, 800.0);
        scenario = scenario with { Forecast = Hourly(36, (_, point) => point with { GlobalTiltedIrradiance = 800.0, ShortwaveRadiation = 5000.0 }) };

        var battery = Assert.IsType<SolarBatteryStrategyConditionResult>(RunAll(Feature(10.0), Now, scenario)[1]);

        Assert.Equal(Math.Round(24 * 800.0 * KwhPerWatt, 2), battery.TomorrowKwhEstimate);
    }

    [Theory]
    [InlineData(10.0, 12.01, 10.0, BatteryStrategy.FeedIn)]
    [InlineData(10.0, 12.0, 10.0, BatteryStrategy.Normal)]
    [InlineData(10.0, 7.99, 10.0, BatteryStrategy.ChargeBattery)]
    [InlineData(10.0, 8.0, 10.0, BatteryStrategy.Normal)]
    [InlineData(9.99, 20.0, 10.0, BatteryStrategy.Conserve)]
    [InlineData(9.99, 5.0, 10.0, BatteryStrategy.Normal)]
    public void Strategy_boundaries(double today, double tomorrow, double capacity, BatteryStrategy expected)
    {
        Assert.Equal(expected, Features.Conditions.SolarYield.SolarYield.Decide(today, tomorrow, capacity));
    }

    [Fact]
    public void No_battery_result_without_satellite_data_for_today()
    {
        var scenario = BatteryScenario(800.0, 800.0) with
        {
            Satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Now, TimeResolution.Hourly, ImmutableArray<SatelliteRadiationPoint>.Empty),
        };

        Assert.IsType<SolarYieldResult>(Assert.Single(RunAll(Feature(10.0), Now, scenario)));
    }

    [Fact]
    public void No_battery_result_without_forecast_irradiance_for_tomorrow()
    {
        var scenario = BatteryScenario(800.0, 800.0) with { Forecast = Hourly(36, (_, point) => point with { GlobalTiltedIrradiance = null }) };

        Assert.IsType<SolarYieldResult>(Assert.Single(RunAll(Feature(10.0), Now, scenario)));
    }
}
