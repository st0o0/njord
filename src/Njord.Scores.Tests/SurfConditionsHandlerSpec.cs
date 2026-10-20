using Microsoft.Extensions.Time.Testing;
using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.MarineFixtures;

namespace Njord.Scores.Tests;

public sealed class SurfConditionsHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);
    private static readonly SurfConditionsHandler Handler = new();

    private static MarinePoint Swell() =>
        new(Now, SwellWaveHeight: 1.8, SwellWavePeriod: 14.0, SwellWaveDirection: 270.0);

    [Fact]
    public void Swell_data_with_offshore_wind_raises_an_excellent_surf_event()
    {
        var weather = new DomainForecast<WeatherPoint>(DomainKind.Weather, Now, TimeResolution.Hourly,
            [new WeatherPoint(Now, WindDirection: 90.0)]);
        var current = new LocationForecast(Lucerne, Weather: weather, Marine: MarineForecast(Swell()));

        var surf = Assert.IsType<ConditionResult>(Assert.Single(Handler.Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, surf.Location),
            () => Assert.Equal("surf", surf.WireKey),
            () => Assert.Equal("100", surf.Data["score"]),
            () => Assert.Equal("excellent", surf.State),
            () => Assert.Equal("1.8", surf.Data["swell_height"]),
            () => Assert.Equal("14", surf.Data["swell_period"]),
            () => Assert.Equal("true", surf.Data["wind_considered"]),
            () => Assert.Equal("offshore", surf.Data["wind"]),
            () => Assert.Equal(FeatureData.Time(Now), surf.Data["best_start"]));
    }

    [Fact]
    public void Missing_wind_data_still_produces_a_swell_only_event()
    {
        var current = new LocationForecast(Lucerne, Marine: MarineForecast(Swell()));

        var surf = Assert.IsType<ConditionResult>(Assert.Single(Handler.Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal("false", surf.Data["wind_considered"]),
            () => Assert.False(surf.Data.ContainsKey("wind")));
    }

    [Fact]
    public void Missing_marine_or_swell_data_raises_nothing()
    {
        Assert.Multiple(
            () => Assert.Empty(Handler.Handle(new LocationForecast(Lucerne), null, Time)),
            () => Assert.Empty(Handler.Handle(new LocationForecast(Lucerne, Marine: MarineForecast(Point(0))), null, Time)));
    }

    [Fact]
    public void The_handler_is_keyed_to_the_surf_toggle()
    {
        Assert.Multiple(
            () => Assert.Equal("Marine:Surf", Handler.SectionKey()),
            () => Assert.Equal("surf", Handler.WireKey()));
    }

    [Fact]
    public void The_handler_requires_swell_height_and_period_and_treats_wind_inputs_as_optional()
    {
        Assert.Equal(
            new DomainRequirements(
                Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
                OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
                OptionalWeather: WeatherParams.WindDirection),
            Handler.Feature.Requirements);
    }

    [Fact]
    public void The_swimming_handler_requirements_stay_untouched()
    {
        Assert.Equal(
            new DomainRequirements(Marine: MarineParams.WaveHeight, OptionalMarine: MarineParams.WaterTemperature),
            new SwimmingConditionsHandler(new SwimmingConditionsRule(), Njord.Tests.Shared.AlertRuleFixture.OptionsWith(true)).Feature.Requirements);
    }
}
