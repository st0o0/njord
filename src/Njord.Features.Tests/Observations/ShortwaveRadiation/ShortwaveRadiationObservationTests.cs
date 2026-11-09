using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Observations.ShortwaveRadiation;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.ShortwaveRadiation;

public sealed class ShortwaveRadiationObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static ShortwaveRadiationObservation Rule() => new(Options.Create(new ShortwaveRadiationObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        Forecast: new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, [new ForecastPoint(_now, ShortwaveRadiation: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(450.0)));

        Assert.Equal("ShortwaveRadiationObservation", result.FeatureName);
        Assert.Equal(450.0, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
