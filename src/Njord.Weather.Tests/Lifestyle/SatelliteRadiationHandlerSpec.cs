using Njord.Core.Configuration;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Solar;
using Njord.Messages.Pipeline;
using Njord.Weather.Lifestyle;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Weather.Tests.Lifestyle.LifestyleFixture;

namespace Njord.Weather.Tests.Lifestyle;

public sealed class SatelliteRadiationHandlerSpec
{
    private static readonly SatelliteRadiationHandler Handler = new();

    private static DomainForecast<SatelliteRadiationPoint> Observed(params (int HoursAgo, double? Gti, double? Ghi)[] hours) =>
        new(DomainKind.Satellite, Now, TimeResolution.Hourly,
            [.. hours.OrderByDescending(h => h.HoursAgo).Select(h => new SatelliteRadiationPoint(Now.AddHours(-h.HoursAgo), GHI: h.Ghi, GTI: h.Gti))]);

    [Fact]
    public void Handle_reports_the_newest_observed_hour()
    {
        var forecast = new LocationForecast(Lucerne, Satellite: Observed((2, 300, 250), (1, 450, 380), (0, 500, 410)));

        var events = Handler.Handle(forecast, null, Time);

        var computed = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("500", computed.State),
            () => Assert.Equal(FeatureData.Time(Now), computed.Data["observed_at"]),
            () => Assert.Equal("500", computed.Data["gti"]),
            () => Assert.Equal("410", computed.Data["ghi"]),
            () => Assert.False(computed.Data.ContainsKey("blended_kwh_per_m2")));
    }

    [Fact]
    public void Handle_blends_the_observed_hour_with_the_ensemble_for_the_next_24_hours()
    {
        var weather = Day((_, point) => point with { GlobalTiltedIrradiance = 100.0 });
        var forecast = new LocationForecast(Lucerne, weather, Satellite: Observed((0, 600, null)));

        var events = Handler.Handle(forecast, null, Time);

        var computed = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Equal(FeatureData.Number(Math.Round((600.0 + 23 * 100.0) / 1000.0, 2)), computed.Data["blended_kwh_per_m2"]);
    }

    [Fact]
    public void Handle_ignores_satellite_data_older_than_three_hours()
    {
        var forecast = new LocationForecast(Lucerne, Satellite: Observed((4, 300, 250)));

        Assert.Empty(Handler.Handle(forecast, null, Time));
    }

    [Fact]
    public void Handle_ignores_observed_hours_without_tilted_irradiance()
    {
        var forecast = new LocationForecast(Lucerne, Satellite: Observed((0, null, 250)));

        Assert.Empty(Handler.Handle(forecast, null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_satellite_data()
    {
        Assert.Empty(Handler.Handle(new LocationForecast(Lucerne), null, Time));
    }

    [Fact]
    public void The_feature_is_off_unless_configured_and_reads_the_satellite_domain()
    {
        var on = new FeatureOptions();
        on.Set("Energy", "SatelliteRadiation", true);

        Assert.Multiple(
            () => Assert.Empty(FeatureResolution.EnabledHandlers(new FeatureOptions(), [Handler])),
            () => Assert.Single(FeatureResolution.EnabledHandlers(on, [Handler])),
            () => Assert.Equal([DomainKind.Satellite], FeatureResolution.RequiredDomains(on, [Handler])));
    }
}
