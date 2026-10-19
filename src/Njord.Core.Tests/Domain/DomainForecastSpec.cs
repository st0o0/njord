using System.Collections.Immutable;
using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Core.Tests.Domain;

public sealed class DomainForecastSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Carries_metadata_and_computes_valid_range_from_points()
    {
        var first = Run.AddHours(6);
        var last = Run.AddHours(8);
        var forecast = new DomainForecast<FloodPoint>(
            DomainKind.Flood,
            Run,
            TimeResolution.Hourly,
            [new FloodPoint(first, 1), new FloodPoint(Run.AddHours(7), 2), new FloodPoint(last, 3)]);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, forecast.Domain),
            () => Assert.Equal(Run, forecast.ModelRun),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(3, forecast.Points.Length),
            () => Assert.Equal(first, forecast.FirstValid),
            () => Assert.Equal(last, forecast.LastValid));
    }

    [Fact]
    public void Empty_points_yield_default_valid_range()
    {
        var forecast = new DomainForecast<FloodPoint>(
            DomainKind.Flood, Run, TimeResolution.Hourly, ImmutableArray<FloodPoint>.Empty);

        Assert.Multiple(
            () => Assert.Equal(default, forecast.FirstValid),
            () => Assert.Equal(default, forecast.LastValid));
    }

    [Fact]
    public void Is_usable_through_the_non_generic_interface()
    {
        IDomainForecast forecast = new DomainForecast<SolarPoint>(
            DomainKind.Solar, Run, TimeResolution.Hourly, [new SolarPoint(Run, 0, 0, 0, 0)]);

        Assert.Multiple(
            () => Assert.Equal(DomainKind.Solar, forecast.Domain),
            () => Assert.Equal(Run, forecast.FirstValid));
    }
}
