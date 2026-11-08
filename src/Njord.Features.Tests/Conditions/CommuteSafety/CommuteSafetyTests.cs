using Microsoft.Extensions.Options;
using Njord.Features.Conditions.CommuteSafety;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Conditions.CommuteSafety;

public sealed class CommuteSafetyTests
{
    private static Features.Conditions.CommuteSafety.CommuteSafety Feature(bool enabled = true) => new(Options.Create(new CommuteSafetyConfig { Enabled = enabled }));

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
    public void A_disabled_feature_reports_itself_as_disabled()
    {
        Assert.False(Feature(enabled: false).Enabled);
    }

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
}
