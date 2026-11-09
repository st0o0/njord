using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Observations.Evapotranspiration;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.Evapotranspiration;

public sealed class EvapotranspirationObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static EvapotranspirationObservation Rule() => new(Options.Create(new EvapotranspirationObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        Forecast: new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, [new ForecastPoint(_now, Evapotranspiration: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(3.2)));

        Assert.Equal("EvapotranspirationObservation", result.FeatureName);
        Assert.Equal(3.2, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
