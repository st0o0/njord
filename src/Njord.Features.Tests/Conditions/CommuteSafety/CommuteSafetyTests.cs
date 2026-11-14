using Microsoft.Extensions.Options;
using Njord.Features.Conditions.CommuteSafety;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.CommuteSafety;

public sealed class CommuteSafetyTests
{
    private static Features.Conditions.CommuteSafety.CommuteSafety Feature(CommuteSafetyConfig? cfg = null) => new(Options.Create(cfg ?? new CommuteSafetyConfig()));

    private static Features.Conditions.CommuteSafety.CommuteSafety Feature(bool enabled) => Feature(new CommuteSafetyConfig { Enabled = enabled });

    [Fact]
    public void Benign_weather_is_reported_as_safe()
    {
        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(Day())));

        Assert.Multiple(
            () => Assert.Equal(CommuteLevel.Safe, safety.Level),
            () => Assert.Empty(safety.Hazards));
    }

    [Fact]
    public void Ice_is_dangerous()
    {
        var weather = Uniform(p => p with { Temperature = 0.0, DewPoint = -0.5 });

        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(CommuteLevel.Dangerous, safety.Level),
            () => Assert.Equal([CommuteHazard.Ice], safety.Hazards));
    }

    [Fact]
    public void Strong_wind_alone_calls_for_caution()
    {
        var weather = Uniform(p => p with { WindSpeed = 20.0 });

        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(CommuteLevel.Caution, safety.Level),
            () => Assert.Equal([CommuteHazard.StrongWind], safety.Hazards));
    }

    [Fact]
    public void Hazards_beyond_the_next_two_hours_are_ignored()
    {
        var weather = Day((hour, p) => hour >= 2 ? p with { Precipitation = 8.0 } : p);

        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Equal(CommuteLevel.Safe, safety.Level);
    }

    [Fact]
    public void A_disabled_feature_reports_itself_as_disabled() => Assert.False(Feature(enabled: false).Enabled);

    [Fact]
    public void Fog_alone_calls_for_caution()
    {
        var weather = Uniform(p => p with
        {
            Temperature = 8.0,
            DewPoint = 7.0,
            Humidity = 96.0,
            Precipitation = 0.0,
            WindSpeed = 2.0,
        });

        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(CommuteLevel.Caution, safety.Level),
            () => Assert.Contains(CommuteHazard.Fog, safety.Hazards));
    }

    [Fact]
    public void Heavy_rain_alone_calls_for_caution()
    {
        var weather = Uniform(p => p with
        {
            Temperature = 10.0,
            DewPoint = 5.0,
            Humidity = 70.0,
            Precipitation = 6.0,
            WindSpeed = 2.0,
        });

        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(CommuteLevel.Caution, safety.Level),
            () => Assert.Contains(CommuteHazard.HeavyRain, safety.Hazards));
    }

    [Fact]
    public void Multiple_non_ice_hazards_are_dangerous()
    {
        var weather = Uniform(p => p with
        {
            Temperature = 8.0,
            DewPoint = 7.0,
            Humidity = 96.0,
            Precipitation = 6.0,
            WindSpeed = 2.0,
        });

        var safety = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Equal(CommuteLevel.Dangerous, safety.Level);
    }

    [Fact]
    public void Raised_ice_threshold_widens_ice_detection()
    {
        var weather = Uniform(p => p with { Temperature = 2.5, DewPoint = 1.5 });

        Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)), exactMatch: false);
        var defaultResult = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));
        Assert.DoesNotContain(CommuteHazard.Ice, defaultResult.Hazards);

        var custom = Feature(new CommuteSafetyConfig { IceTemperatureCelsius = 3.0 });
        var customResult = Assert.IsType<CommuteSafetyResult>(Run(custom, Now, ForecastOnly(weather)));
        Assert.Contains(CommuteHazard.Ice, customResult.Hazards);
    }

    [Fact]
    public void Lowered_wind_threshold_catches_moderate_wind()
    {
        var weather = Uniform(p => p with { WindSpeed = 10.0 });

        var defaultResult = Assert.IsType<CommuteSafetyResult>(Run(Feature(), Now, ForecastOnly(weather)));
        Assert.DoesNotContain(CommuteHazard.StrongWind, defaultResult.Hazards);

        var custom = Feature(new CommuteSafetyConfig { StrongWindKmh = 30.0 });
        var customResult = Assert.IsType<CommuteSafetyResult>(Run(custom, Now, ForecastOnly(weather)));
        Assert.Contains(CommuteHazard.StrongWind, customResult.Hazards);
    }
}
