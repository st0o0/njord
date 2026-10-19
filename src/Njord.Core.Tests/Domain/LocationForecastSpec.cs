using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Core.Tests.Domain;

public sealed class LocationForecastSpec
{
    private static readonly Location Lucerne = new("Lucerne", 47.05, 8.31);
    private static readonly DateTimeOffset Run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static DomainForecast<T> Empty<T>(DomainKind kind) where T : IHasValidTime =>
        new(kind, Run, TimeResolution.Hourly, []);

    [Fact]
    public void Should_start_without_any_domain_data()
    {
        var forecast = new LocationForecast(Lucerne);

        Assert.All(Enum.GetValues<DomainKind>(), kind => Assert.Null(forecast.Get(kind)));
    }

    [Theory]
    [InlineData(DomainKind.Weather)]
    [InlineData(DomainKind.AirQuality)]
    [InlineData(DomainKind.Flood)]
    [InlineData(DomainKind.Solar)]
    [InlineData(DomainKind.Marine)]
    public void Should_store_data_under_its_domain_and_leave_others_untouched(DomainKind kind)
    {
        IDomainForecast data = kind switch
        {
            DomainKind.Weather => Empty<WeatherPoint>(kind),
            DomainKind.AirQuality => Empty<AirQualityPoint>(kind),
            DomainKind.Flood => Empty<FloodPoint>(kind),
            DomainKind.Solar => Empty<SolarPoint>(kind),
            _ => Empty<MarinePoint>(kind),
        };

        var updated = new LocationForecast(Lucerne).With(kind, data);

        Assert.Multiple(
            () => Assert.Same(data, updated.Get(kind)),
            () => Assert.Single(Enum.GetValues<DomainKind>(), k => updated.Get(k) is not null));
    }

    [Fact]
    public void Should_replace_existing_domain_data_without_mutating_the_original()
    {
        var first = Empty<WeatherPoint>(DomainKind.Weather);
        var second = first with { ModelRun = Run.AddHours(1) };
        var original = new LocationForecast(Lucerne).With(DomainKind.Weather, first);

        var updated = original.With(DomainKind.Weather, second);

        Assert.Multiple(
            () => Assert.Same(first, original.Weather),
            () => Assert.Same(second, updated.Weather));
    }

    [Fact]
    public void Should_reject_data_of_a_different_domain_type()
    {
        var flood = Empty<FloodPoint>(DomainKind.Flood);

        var ex = Assert.Throws<ArgumentException>(() => new LocationForecast(Lucerne).With(DomainKind.Weather, flood));

        Assert.Equal("forecast", ex.ParamName);
    }

    [Fact]
    public void Should_throw_for_an_undefined_domain()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LocationForecast(Lucerne).Get((DomainKind)99));

        Assert.Equal("domain", ex.ParamName);
    }
}
