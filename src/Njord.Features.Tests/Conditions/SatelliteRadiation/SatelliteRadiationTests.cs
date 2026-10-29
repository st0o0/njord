using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Forecast;
using Njord.Core.Solar;
using Njord.Features.Conditions.SatelliteRadiation;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.SatelliteRadiation;

public sealed class SatelliteRadiationTests
{
    private static Features.Conditions.SatelliteRadiation.SatelliteRadiation Feature() => new(Options.Create(new SatelliteRadiationConfig { Enabled = true }));

    private static DomainForecast<SatelliteRadiationPoint> Observed(params (int HoursAgo, double? Gti, double? Ghi)[] hours) =>
        new(DomainKind.Satellite, Now, TimeResolution.Hourly,
            [.. hours.OrderByDescending(h => h.HoursAgo).Select(h => new SatelliteRadiationPoint(Now.AddHours(-h.HoursAgo), GHI: h.Ghi, GTI: h.Gti))]);

    [Fact]
    public void The_newest_observed_hour_is_reported()
    {
        var forecast = ForecastOnly(Day()) with { Satellite = Observed((2, 300, 250), (1, 450, 380), (0, 500, 410)) };

        var computed = Assert.IsType<SatelliteRadiationResult>(Run(Feature(), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(Now, computed.ObservedAt),
            () => Assert.Equal(500.0, computed.Gti),
            () => Assert.Equal(410.0, computed.Ghi));
    }

    [Fact]
    public void The_observed_hour_is_blended_with_the_ensemble_for_the_next_24_hours()
    {
        var forecast = ForecastOnly(Day((_, point) => point with { GlobalTiltedIrradiance = 100.0 }))
            with
        { Satellite = Observed((0, 600, null)) };

        var computed = Assert.IsType<SatelliteRadiationResult>(Run(Feature(), Now, forecast));

        Assert.Equal(Math.Round((600.0 + 23 * 100.0) / 1000.0, 2), computed.BlendedKwhPerSquareMeter);
    }

    [Fact]
    public void Satellite_data_older_than_three_hours_is_ignored()
    {
        var forecast = ForecastOnly(Day()) with { Satellite = Observed((4, 300, 250)) };

        Assert.Null(Run(Feature(), Now, forecast));
    }

    [Fact]
    public void Observed_hours_without_tilted_irradiance_are_ignored()
    {
        var forecast = ForecastOnly(Day()) with { Satellite = Observed((0, null, 250)) };

        Assert.Null(Run(Feature(), Now, forecast));
    }

    [Fact]
    public void Nothing_is_reported_without_satellite_data()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Day())));
    }

    [Fact]
    public void The_feature_is_off_by_default()
    {
        Assert.False(Feature().Descriptor.DefaultEnabled);
    }
}
