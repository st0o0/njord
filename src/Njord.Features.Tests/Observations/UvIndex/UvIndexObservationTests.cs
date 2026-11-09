using Microsoft.Extensions.Options;
using Njord.Core;

using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Observations.UvIndex;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.UvIndex;

public sealed class UvIndexObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static UvIndexObservation Rule() => new(Options.Create(new UvIndexObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, [new ForecastPoint(_now, UvIndex: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(6.5)));

        Assert.Equal(6.5, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
