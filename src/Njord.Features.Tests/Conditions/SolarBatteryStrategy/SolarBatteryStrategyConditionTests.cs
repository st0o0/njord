using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Solar;
using Njord.Features.Conditions.SolarBatteryStrategy;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.SolarBatteryStrategy;

public sealed class SolarBatteryStrategyConditionTests
{
    // 13 satellite hours today (00:00 to 12:00) and 24 forecast hours tomorrow, 0.00425 kWh per W with the defaults.
    private const double KwhPerWatt = 5.0 / 1000.0 * 0.85;

    private static SolarBatteryStrategyCondition Feature() => new(Options.Create(new SolarBatteryStrategyConfig { Enabled = true }));

    private static LocationForecast Scenario(double todayGti, double tomorrowRadiation)
    {
        var forecast = Hourly(36, (hour, point) => point with { ShortwaveRadiation = tomorrowRadiation });
        var satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Now, TimeResolution.Hourly,
            [.. Enumerable.Range(0, 13).Select(hour => new SatelliteRadiationPoint(Now.AddHours(hour - 12), GTI: todayGti))]);
        return new(Lucerne, forecast, Satellite: satellite);
    }

    private static SolarBatteryStrategyConditionResult Evaluate(double todayGti, double tomorrowRadiation) =>
        Assert.IsType<SolarBatteryStrategyConditionResult>(Run(Feature(), Now, Scenario(todayGti, tomorrowRadiation)));

    [Fact]
    public void A_sunny_tomorrow_after_a_surplus_today_feeds_in()
    {
        var result = Evaluate(800.0, 800.0 * 2);

        Assert.Multiple(
            () => Assert.Equal(SolarBatteryStrategyCondition.FeedIn, result.Strategy),
            () => Assert.Equal(Math.Round(13 * 800.0 * KwhPerWatt, 2), result.TodayKwh),
            () => Assert.Equal(Math.Round(24 * 1600.0 * KwhPerWatt, 2), result.TomorrowKwhEstimate));
    }

    [Fact]
    public void A_cloudy_tomorrow_after_a_surplus_today_charges_the_battery()
    {
        Assert.Equal(SolarBatteryStrategyCondition.ChargeBattery, Evaluate(800.0, 100.0).Strategy);
    }

    [Fact]
    public void A_sunny_tomorrow_after_a_low_today_conserves()
    {
        Assert.Equal(SolarBatteryStrategyCondition.Conserve, Evaluate(50.0, 800.0).Strategy);
    }

    [Fact]
    public void Similar_days_are_normal()
    {
        Assert.Equal(SolarBatteryStrategyCondition.Normal, Evaluate(800.0, 800.0 * 13 / 24).Strategy);
    }

    [Theory]
    [InlineData(10.0, 12.01, 10.0, SolarBatteryStrategyCondition.FeedIn)]
    [InlineData(10.0, 12.0, 10.0, SolarBatteryStrategyCondition.Normal)]
    [InlineData(10.0, 7.99, 10.0, SolarBatteryStrategyCondition.ChargeBattery)]
    [InlineData(10.0, 8.0, 10.0, SolarBatteryStrategyCondition.Normal)]
    [InlineData(9.99, 20.0, 10.0, SolarBatteryStrategyCondition.Conserve)]
    [InlineData(9.99, 5.0, 10.0, SolarBatteryStrategyCondition.Normal)]
    public void Strategy_boundaries(double today, double tomorrow, double capacity, string expected)
    {
        Assert.Equal(expected, SolarBatteryStrategyCondition.Decide(today, tomorrow, capacity));
    }

    [Fact]
    public void Nothing_is_reported_without_satellite_data_for_today()
    {
        var scenario = Scenario(800.0, 800.0) with
        {
            Satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Now, TimeResolution.Hourly, ImmutableArray<SatelliteRadiationPoint>.Empty),
        };

        Assert.Null(Run(Feature(), Now, scenario));
    }

    [Fact]
    public void Nothing_is_reported_without_forecast_radiation_for_tomorrow()
    {
        var scenario = Scenario(800.0, 800.0) with { Forecast = Hourly(36, (_, point) => point with { ShortwaveRadiation = null }) };

        Assert.Null(Run(Feature(), Now, scenario));
    }

    [Fact]
    public void The_requirements_ask_for_shortwave_radiation_and_tilted_satellite_irradiance()
    {
        Assert.Equal(
            new DomainRequirements(Forecast: ForecastParams.ShortwaveRadiation, Satellite: SatelliteParams.GlobalTilted),
            Feature().Descriptor.Requirements);
    }
}
