using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Configuration;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class SolarYieldHandlerSpec
{
    private static SolarYieldHandler HandlerWith(Action<SolarYieldOptions>? configure = null)
    {
        var options = new SolarYieldOptions { Tilt = 35 };
        configure?.Invoke(options);
        return new SolarYieldHandler(Options.Create(options));
    }

    private static LocationForecast Sunny() => WeatherOnly(Day((hour, point) => point with
    {
        DirectRadiation = hour == 3 ? 800.0 : 100.0,
        DiffuseRadiation = 50.0,
    }));

    [Fact]
    public void Handle_sums_direct_and_diffuse_radiation_scaled_by_efficiency()
    {
        var events = HandlerWith().Handle(Sunny(), null, Time);

        var yield = Assert.IsType<PipelineEvent.SolarYieldComputed>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(Math.Round((23 * 150.0 + 850.0) / 1000.0 * SolarYieldHandler.SystemEfficiency, 2), yield.KwhPerKwp),
            () => Assert.Equal(Now.AddHours(3).Hour, yield.PeakHour));
    }

    [Fact]
    public void Handle_lowers_the_yield_for_an_off_south_panel()
    {
        var south = Assert.IsType<PipelineEvent.SolarYieldComputed>(Assert.Single(HandlerWith().Handle(Sunny(), null, Time)));
        var north = Assert.IsType<PipelineEvent.SolarYieldComputed>(
            Assert.Single(HandlerWith(o => o.Azimuth = 0).Handle(Sunny(), null, Time)));

        Assert.True(north.KwhPerKwp < south.KwhPerKwp);
    }

    [Fact]
    public void Handle_returns_nothing_without_radiation_data()
    {
        Assert.Empty(HandlerWith().Handle(WeatherOnly(Day()), null, Time));
    }
}
