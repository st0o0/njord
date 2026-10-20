using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;
using static Njord.Marine.Tests.MarineFixtures;

namespace Njord.Marine.Tests;

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

        var surf = Assert.IsType<PipelineEvent.SurfConditionsComputed>(Assert.Single(Handler.Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, surf.Location),
            () => Assert.Equal(100, surf.Score),
            () => Assert.Equal("excellent", surf.Rating),
            () => Assert.Equal(1.8, surf.SwellHeight),
            () => Assert.Equal(14.0, surf.SwellPeriod),
            () => Assert.Equal("offshore", surf.WindRelation),
            () => Assert.Equal(Now, surf.BestStart));
    }

    [Fact]
    public void Missing_wind_data_still_produces_a_swell_only_event()
    {
        var current = new LocationForecast(Lucerne, Marine: MarineForecast(Swell()));

        var surf = Assert.IsType<PipelineEvent.SurfConditionsComputed>(Assert.Single(Handler.Handle(current, null, Time)));

        Assert.Null(surf.WindRelation);
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
        Assert.Equal("SurfConditions", Handler.ConfigKey);
    }

    [Fact]
    public void The_handler_requires_swell_height_and_period_and_treats_wind_inputs_as_optional()
    {
        Assert.Equal(
            new DomainRequirements(
                Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
                OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
                OptionalWeather: WeatherParams.WindDirection),
            Handler.Requirements);
    }

    [Fact]
    public void The_swimming_handler_requirements_stay_untouched()
    {
        Assert.Equal(
            new DomainRequirements(Marine: MarineParams.WaveHeight, OptionalMarine: MarineParams.WaterTemperature),
            new SwimmingConditionsHandler(new SwimmingConditionsRule(), Njord.Tests.Shared.AlertRuleFixture.OptionsWith(true)).Requirements);
    }
}
