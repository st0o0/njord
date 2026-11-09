using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Core.Marine;

namespace Njord.Core.Tests.Domain;

public sealed class LocationForecastTests
{
    private static readonly Location _lucerne = new("Lucerne", 47.05, 8.31);
    private static readonly DateTimeOffset _run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static DomainForecast<T> Empty<T>(DomainKind kind) where T : IHasValidTime =>
        new(kind, _run, TimeResolution.Hourly, []);

    [Fact]
    public void Should_start_without_any_domain_data()
    {
        var forecast = new LocationForecast(_lucerne);

        Assert.All(Enum.GetValues<DomainKind>(), kind => Assert.Null(forecast.Get(kind)));
    }

    [Theory]
    [InlineData(DomainKind.Forecast)]
    [InlineData(DomainKind.AirQuality)]
    [InlineData(DomainKind.Flood)]
    [InlineData(DomainKind.Marine)]
    public void Should_store_data_under_its_domain_and_leave_others_untouched(DomainKind kind)
    {
        IDomainForecast data = kind switch
        {
            DomainKind.Forecast => Empty<ForecastPoint>(kind),
            DomainKind.AirQuality => Empty<AirQualityPoint>(kind),
            DomainKind.Flood => Empty<FloodPoint>(kind),
            _ => Empty<MarinePoint>(kind),
        };

        var updated = new LocationForecast(_lucerne).With(kind, data);

        Assert.Multiple(
            () => Assert.Same(data, updated.Get(kind)),
            () => Assert.Single(Enum.GetValues<DomainKind>(), k => updated.Get(k) is not null));
    }

    [Fact]
    public void Should_replace_existing_domain_data_without_mutating_the_original()
    {
        var first = Empty<ForecastPoint>(DomainKind.Forecast);
        var second = first with { ModelRun = _run.AddHours(1) };
        var original = new LocationForecast(_lucerne).With(DomainKind.Forecast, first);

        var updated = original.With(DomainKind.Forecast, second);

        Assert.Multiple(
            () => Assert.Same(first, original.Forecast),
            () => Assert.Same(second, updated.Forecast));
    }

    [Fact]
    public void Should_reject_data_of_a_different_domain_type()
    {
        var flood = Empty<FloodPoint>(DomainKind.Flood);

        var ex = Assert.Throws<ArgumentException>(() => new LocationForecast(_lucerne).With(DomainKind.Forecast, flood));

        Assert.Equal("forecast", ex.ParamName);
    }

    [Fact]
    public void Should_throw_for_an_undefined_domain()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LocationForecast(_lucerne).Get((DomainKind)99));

        Assert.Equal("domain", ex.ParamName);
    }
}
