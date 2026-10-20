using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class SolarYieldHandlerSpec
{
    private static LocationForecast Sunny() => WeatherOnly(Day((hour, point) => point with
    {
        GlobalTiltedIrradiance = hour == 3 ? 800.0 : 100.0,
    }));

    [Fact]
    public void Handle_sums_global_tilted_irradiance_scaled_by_efficiency()
    {
        var events = new SolarYieldHandler().Handle(Sunny(), null, Time);

        var yield = Assert.IsType<PipelineEvent.SolarYieldComputed>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(Math.Round((23 * 100.0 + 800.0) / 1000.0 * SolarYieldHandler.SystemEfficiency, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void Handle_ignores_direct_and_diffuse_radiation_without_tilted_irradiance()
    {
        var forecast = WeatherOnly(Day((_, point) => point with { DirectRadiation = 500.0, DiffuseRadiation = 50.0 }));

        Assert.Empty(new SolarYieldHandler().Handle(forecast, null, Time));
    }

    private static LocationForecast WithSatellite(params (int HoursAgo, double Gti)[] hours) =>
        Sunny() with
        {
            Satellite = new DomainForecast<SatelliteRadiationPoint>(DomainKind.Satellite, Now, TimeResolution.Hourly,
                [.. hours.OrderByDescending(h => h.HoursAgo).Select(h => new SatelliteRadiationPoint(Now.AddHours(-h.HoursAgo), GTI: h.Gti))]),
        };

    [Fact]
    public void Handle_uses_satellite_irradiance_up_to_the_newest_observation_and_the_ensemble_afterwards()
    {
        var events = new SolarYieldHandler().Handle(WithSatellite((1, 50.0), (0, 400.0)), null, Time);

        var yield = Assert.IsType<PipelineEvent.SolarYieldComputed>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(Math.Round((400.0 + 800.0 + 22 * 100.0) / 1000.0 * SolarYieldHandler.SystemEfficiency, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void Handle_lets_a_measured_peak_win_the_peak_hour()
    {
        var events = new SolarYieldHandler().Handle(WithSatellite((0, 1000.0)), null, Time);

        var yield = Assert.IsType<PipelineEvent.SolarYieldComputed>(Assert.Single(events));
        Assert.Equal(Now.Hour, yield.PeakHour);
    }

    [Fact]
    public void Handle_ignores_satellite_data_older_than_three_hours()
    {
        var withStale = new SolarYieldHandler().Handle(WithSatellite((4, 900.0)), null, Time);
        var ensembleOnly = new SolarYieldHandler().Handle(Sunny(), null, Time);

        Assert.Equal(ensembleOnly, withStale);
    }

    [Fact]
    public void Requirements_ask_for_global_tilted_irradiance()
    {
        Assert.Equal(WeatherParams.GlobalTiltedIrradiance, new SolarYieldHandler().Feature.Requirements.Weather);
    }

    [Fact]
    public void Handle_returns_nothing_without_radiation_data()
    {
        Assert.Empty(new SolarYieldHandler().Handle(WeatherOnly(Day()), null, Time));
    }
}
