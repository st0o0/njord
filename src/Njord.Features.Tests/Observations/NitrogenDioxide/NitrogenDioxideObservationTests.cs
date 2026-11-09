using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Observations.NitrogenDioxide;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.NitrogenDioxide;

public sealed class NitrogenDioxideObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static NitrogenDioxideObservation Rule() => new(Options.Create(new NitrogenDioxideObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        AirQuality: new DomainForecast<AirQualityPoint>(DomainKind.AirQuality, _now, TimeResolution.Hourly, [new AirQualityPoint(_now, NitrogenDioxide: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(40.0)));

        Assert.Equal("NitrogenDioxideObservation", result.FeatureName);
        Assert.Equal(40.0, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
