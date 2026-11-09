using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Observations.DewPoint;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.DewPoint;

public sealed class DewPointObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static DewPointObservation Rule() => new(Options.Create(new DewPointObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        Forecast: new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, [new ForecastPoint(_now, DewPoint: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(12.5)));

        Assert.Equal("DewPointObservation", result.FeatureName);
        Assert.Equal(12.5, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
