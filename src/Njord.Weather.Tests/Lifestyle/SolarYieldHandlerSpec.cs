using Njord.Core.Domain;
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

    [Fact]
    public void Requirements_ask_for_global_tilted_irradiance()
    {
        Assert.Equal(WeatherParams.GlobalTiltedIrradiance, new SolarYieldHandler().Requirements.Weather);
    }

    [Fact]
    public void Handle_returns_nothing_without_radiation_data()
    {
        Assert.Empty(new SolarYieldHandler().Handle(WeatherOnly(Day()), null, Time));
    }
}
