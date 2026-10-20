using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.Core.Tests.Domain;

public sealed class DomainRequirementsSpec
{
    [Fact]
    public void Should_union_every_domain_with_the_or_operator()
    {
        var left = new DomainRequirements(Weather: WeatherParam.Temperature, Flood: FloodParam.Discharge);
        var right = new DomainRequirements(
            Weather: WeatherParam.WindSpeed,
            AirQuality: AirQualityParam.Pm25,
            Marine: MarineParam.WaveHeight);

        var union = left | right;

        Assert.Multiple(
            () => Assert.Equal(WeatherParam.Temperature | WeatherParam.WindSpeed, union.Weather),
            () => Assert.Equal(AirQualityParam.Pm25, union.AirQuality),
            () => Assert.Equal(FloodParam.Discharge, union.Flood),
            () => Assert.Equal(MarineParam.WaveHeight, union.Marine));
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
            Weather: weather ? WeatherParam.Temperature : default,
            AirQuality: air ? AirQualityParam.Pm25 : default,
            Flood: flood ? FloodParam.Discharge : default,
            Marine: marine ? MarineParam.WaveHeight : default);

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
            WeatherParam.Temperature, AirQualityParam.Pm25, FloodParam.Discharge, MarineParam.WaveHeight);

        Assert.False(everything.Requires(DomainKind.Solar));
    }

    [Fact]
    public void Should_compare_equal_when_all_params_match()
    {
        Assert.Equal(
            new DomainRequirements(Weather: WeatherParam.Temperature),
            new DomainRequirements(Weather: WeatherParam.Temperature));
    }
}
