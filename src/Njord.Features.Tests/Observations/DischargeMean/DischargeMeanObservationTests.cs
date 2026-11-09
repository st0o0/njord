using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Features.Observations.DischargeMean;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.DischargeMean;

public sealed class DischargeMeanObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static DischargeMeanObservation Rule() => new(Options.Create(new DischargeMeanObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        Flood: new DomainForecast<FloodPoint>(DomainKind.Flood, _now, TimeResolution.Hourly, [new FloodPoint(_now, DischargeMean: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(125.0)));

        Assert.Equal("DischargeMeanObservation", result.FeatureName);
        Assert.Equal(125.0, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
