using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Marine;
using Njord.Features.Observations.OceanCurrentVelocity;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Observations.OceanCurrentVelocity;

public sealed class OceanCurrentVelocityObservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static OceanCurrentVelocityObservation Rule() => new(Options.Create(new OceanCurrentVelocityObservationConfig { Enabled = true }));

    private static LocationForecast Build(double? value) => new(
        new Location("Vienna", 48.2, 16.37),
        Marine: new DomainForecast<MarinePoint>(DomainKind.Marine, _now, TimeResolution.Hourly, [new MarinePoint(_now, OceanCurrentVelocity: value)]));

    [Fact]
    public void Current_value_is_reported()
    {
        var result = Assert.IsType<ObservationResult>(Run(Rule(), _now, Build(0.35)));

        Assert.Equal("OceanCurrentVelocityObservation", result.FeatureName);
        Assert.Equal(0.35, result.Value);
    }

    [Fact]
    public void Missing_value_yields_null() => Assert.Null(Run(Rule(), _now, Build(null)));
}
