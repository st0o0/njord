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

    private static Features.Conditions.SolarYield.SolarYield Feature() =>
        new(Options.Create(new SolarYieldConfig { Enabled = true, PeakKw = PeakKw, SystemEfficiency = Efficiency }));

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
}
