using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Features.Observations.WavePeriod;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.WavePeriod;

public sealed class WavePeriodObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static WavePeriodObservation Rule() => new(Options.Create(new WavePeriodObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        Marine: new DomainForecast<MarinePoint>(DomainKind.Marine, _now, TimeResolution.Hourly, [new MarinePoint(_now, WavePeriod: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(7.2)));

        Assert.Equal("WavePeriodObservation", result.FeatureName);
        Assert.Equal(7.2, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
