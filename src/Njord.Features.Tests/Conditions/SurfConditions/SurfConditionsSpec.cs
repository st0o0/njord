using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.SurfConditions;
using Njord.Forecast;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.MarineFixtures;

namespace Njord.Features.Tests.Conditions;

public sealed class SurfConditionsSpec
{
    private static SurfConditions Feature() => new(Options.Create(new SurfConditionsConfig { Enabled = true }));

    private static MarinePoint Swell() =>
        new(Now, SwellWaveHeight: 1.8, SwellWavePeriod: 14.0, SwellWaveDirection: 270.0);

    [Fact]
    public void Swell_data_with_offshore_wind_scores_100()
    {
        var weather = new DomainForecast<WeatherPoint>(DomainKind.Weather, Now, TimeResolution.Hourly,
            [new WeatherPoint(Now, WindDirection: 90.0)]);
        var current = new LocationForecast(Lucerne, Weather: weather, Marine: MarineForecast(Swell()));

        var surf = Assert.IsType<SurfConditionsResult>(Run(Feature(), Now, current));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, surf.Location),
            () => Assert.Equal(100, surf.Score),
            () => Assert.Equal(Now, surf.BestStart));
    }

    [Fact]
    public void Missing_marine_or_swell_data_yields_nothing()
    {
        Assert.Multiple(
            () => Assert.Null(Run(Feature(), Now, new LocationForecast(Lucerne))),
            () => Assert.Null(Run(Feature(), Now, new LocationForecast(Lucerne, Marine: MarineForecast(Point(0))))));
    }

    [Fact]
    public void The_feature_requires_swell_height_and_period_and_treats_wind_inputs_as_optional()
    {
        Assert.Equal(
            new DomainRequirements(
                Marine: MarineParams.SwellWaveHeight | MarineParams.SwellWavePeriod,
                OptionalMarine: MarineParams.SwellWaveDirection | MarineParams.WindWaveHeight,
                OptionalWeather: WeatherParams.WindDirection),
            Feature().Descriptor.Requirements);
    }
}
