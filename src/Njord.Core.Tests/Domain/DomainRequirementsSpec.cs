using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Solar;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Domain;

public sealed class DomainRequirementsSpec
{
    [Fact]
    public void Should_union_every_domain_with_the_or_operator()
    {
        var left = new DomainRequirements(Weather: WeatherParams.Temperature, Flood: FloodParams.Discharge);
        var right = new DomainRequirements(
            Weather: WeatherParams.WindSpeed,
            AirQuality: AirQualityParams.Pm25,
            Marine: MarineParams.WaveHeight);

        var union = left | right;

        Assert.Multiple(
            () => Assert.Equal(WeatherParams.Temperature | WeatherParams.WindSpeed, union.Weather),
            () => Assert.Equal(AirQualityParams.Pm25, union.AirQuality),
            () => Assert.Equal(FloodParams.Discharge, union.Flood),
            () => Assert.Equal(MarineParams.WaveHeight, union.Marine));
    }

    [Fact]
    public void Should_union_the_optional_params_separately_from_the_required_ones()
    {
        var left = new DomainRequirements(Weather: WeatherParams.Temperature, OptionalWeather: WeatherParams.UvIndex);
        var right = new DomainRequirements(
            Weather: WeatherParams.WindSpeed,
            OptionalWeather: WeatherParams.SoilTemperature,
            OptionalAirQuality: AirQualityParams.BirchPollen,
            OptionalFlood: FloodParams.DischargeMax,
            OptionalMarine: MarineParams.WaterTemperature);

        var union = left | right;

        Assert.Multiple(
            () => Assert.Equal(WeatherParams.Temperature | WeatherParams.WindSpeed, union.Weather),
            () => Assert.Equal(WeatherParams.UvIndex | WeatherParams.SoilTemperature, union.OptionalWeather),
            () => Assert.Equal(AirQualityParams.BirchPollen, union.OptionalAirQuality),
            () => Assert.Equal(FloodParams.DischargeMax, union.OptionalFlood),
            () => Assert.Equal(MarineParams.WaterTemperature, union.OptionalMarine));
    }

    [Fact]
    public void Should_expose_required_and_optional_params_together_as_all()
    {
        var requirements = new DomainRequirements(
            Weather: WeatherParams.Temperature,
            AirQuality: AirQualityParams.Pm25,
            Flood: FloodParams.Discharge,
            Marine: MarineParams.WaveHeight,
            OptionalWeather: WeatherParams.UvIndex,
            OptionalAirQuality: AirQualityParams.BirchPollen,
            OptionalFlood: FloodParams.DischargeMax,
            OptionalMarine: MarineParams.WaterTemperature);

        Assert.Multiple(
            () => Assert.Equal(WeatherParams.Temperature | WeatherParams.UvIndex, requirements.AllWeather),
            () => Assert.Equal(AirQualityParams.Pm25 | AirQualityParams.BirchPollen, requirements.AllAirQuality),
            () => Assert.Equal(FloodParams.Discharge | FloodParams.DischargeMax, requirements.AllFlood),
            () => Assert.Equal(MarineParams.WaveHeight | MarineParams.WaterTemperature, requirements.AllMarine));
    }

    [Fact]
    public void Should_require_a_domain_that_only_has_optional_params()
    {
        var requirements = new DomainRequirements(OptionalMarine: MarineParams.WaterTemperature);

        Assert.Multiple(
            () => Assert.True(requirements.Requires(DomainKind.Marine)),
            () => Assert.False(requirements.Requires(DomainKind.Weather)));
    }

    [Fact]
    public void Should_require_the_satellite_domain_for_required_or_optional_satellite_params()
    {
        Assert.Multiple(
            () => Assert.True(new DomainRequirements(Satellite: SatelliteParams.GlobalTilted).Requires(DomainKind.Satellite)),
            () => Assert.True(new DomainRequirements(OptionalSatellite: SatelliteParams.GlobalHorizontal).Requires(DomainKind.Satellite)),
            () => Assert.Equal(
                SatelliteParams.GlobalTilted | SatelliteParams.GlobalHorizontal,
                (new DomainRequirements(Satellite: SatelliteParams.GlobalTilted) | new DomainRequirements(OptionalSatellite: SatelliteParams.GlobalHorizontal)).AllSatellite));
    }

    [Fact]
    public void Should_require_nothing_by_default()
    {
        var none = default(DomainRequirements);

        Assert.All(Enum.GetValues<DomainKind>(), domain => Assert.False(none.Requires(domain)));
    }

    [Theory]
    [InlineData(DomainKind.Weather, true, false, false, false)]
    [InlineData(DomainKind.AirQuality, false, true, false, false)]
    [InlineData(DomainKind.Flood, false, false, true, false)]
    [InlineData(DomainKind.Marine, false, false, false, true)]
    public void Should_require_only_the_domains_with_params(DomainKind domain, bool weather, bool air, bool flood, bool marine)
    {
        var requirements = new DomainRequirements(
            Weather: weather ? WeatherParams.Temperature : default,
            AirQuality: air ? AirQualityParams.Pm25 : default,
            Flood: flood ? FloodParams.Discharge : default,
            Marine: marine ? MarineParams.WaveHeight : default);

        Assert.Multiple(
            () => Assert.True(requirements.Requires(domain)),
            () => Assert.All(
                Enum.GetValues<DomainKind>().Where(d => d != domain),
                other => Assert.False(requirements.Requires(other))));
    }

    [Fact]
    public void Should_never_require_the_solar_domain()
    {
        var everything = new DomainRequirements(
            WeatherParams.Temperature, AirQualityParams.Pm25, FloodParams.Discharge, MarineParams.WaveHeight);

        Assert.False(everything.Requires(DomainKind.Seasonal));
    }

    [Fact]
    public void Should_compare_equal_when_all_params_match()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParams.Temperature),
            new DomainRequirements(Weather: WeatherParams.Temperature));
    }
}
