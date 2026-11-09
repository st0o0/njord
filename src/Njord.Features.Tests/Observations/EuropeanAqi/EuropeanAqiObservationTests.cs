using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Observations.EuropeanAqi;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.EuropeanAqi;

public sealed class EuropeanAqiObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static EuropeanAqiObservation Rule() => new(Options.Create(new EuropeanAqiObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        AirQuality: new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, _now, TimeResolution.Hourly, [new AirQualityPoint(_now, EuropeanAqi: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(65.0)));

        Assert.Equal("EuropeanAqiObservation", result.FeatureName);
        Assert.Equal(65.0, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
