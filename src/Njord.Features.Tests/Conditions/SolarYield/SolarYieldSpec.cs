using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.SolarYield;
using Njord.Forecast;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions;

public sealed class SolarYieldSpec
{
    private static SolarYield Feature() => new(Options.Create(new SolarYieldConfig { Enabled = true }));

    private static LocationForecast Sunny() => WeatherOnly(Day((hour, point) => point with
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
        var yield = Assert.IsType<SolarYieldResult>(Assert.Single(Run(Feature(), Now, Sunny())));

        Assert.Multiple(
            () => Assert.Equal(Math.Round((23 * 100.0 + 800.0) / 1000.0 * SolarYield.SystemEfficiency, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void Direct_and_diffuse_radiation_without_tilted_irradiance_yield_nothing()
    {
        var forecast = WeatherOnly(Day((_, point) => point with { DirectRadiation = 500.0, DiffuseRadiation = 50.0 }));

        Assert.Empty(Run(Feature(), Now, forecast));
    }

    [Fact]
    public void Satellite_irradiance_is_used_up_to_the_newest_observation_and_the_ensemble_afterwards()
    {
        var yield = Assert.IsType<SolarYieldResult>(Assert.Single(Run(Feature(), Now, WithSatellite((1, 50.0), (0, 400.0)))));

        Assert.Multiple(
            () => Assert.Equal(Math.Round((400.0 + 800.0 + 22 * 100.0) / 1000.0 * SolarYield.SystemEfficiency, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void A_measured_peak_wins_the_peak_hour()
    {
        var yield = Assert.IsType<SolarYieldResult>(Assert.Single(Run(Feature(), Now, WithSatellite((0, 1000.0)))));

        Assert.Equal(Now.Hour, yield.PeakHour);
    }

    [Fact]
    public void Satellite_data_older_than_three_hours_is_ignored()
    {
        var withStale = Assert.IsType<SolarYieldResult>(Assert.Single(Run(Feature(), Now, WithSatellite((4, 900.0)))));
        var ensembleOnly = Assert.IsType<SolarYieldResult>(Assert.Single(Run(Feature(), Now, Sunny())));

        Assert.Equal(ensembleOnly, withStale);
    }

    [Fact]
    public void The_requirements_ask_for_global_tilted_irradiance()
    {
        Assert.Equal(WeatherParams.GlobalTiltedIrradiance, Feature().Descriptor.Requirements.Weather);
    }

    [Fact]
    public void Nothing_is_reported_without_radiation_data()
    {
        Assert.Empty(Run(Feature(), Now, WeatherOnly(Day())));
    }
}
