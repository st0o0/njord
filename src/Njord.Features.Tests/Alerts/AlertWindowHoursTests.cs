using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Alerts.ClearSkyUv;
using Njord.Features.Alerts.Dust;
using Njord.Features.Alerts.Fog;
using Njord.Features.Alerts.Frost;
using Njord.Features.Alerts.Heat;
using Njord.Features.Alerts.HeavyRain;
using Njord.Features.Alerts.Humidity;
using Njord.Features.Alerts.Ice;
using Njord.Features.Alerts.PressureDrop;
using Njord.Features.Alerts.Snow;
using Njord.Features.Alerts.Storm;
using Njord.Features.Alerts.TropicalNight;
using Njord.Features.Alerts.Visibility;
using Njord.Features.Alerts.WetBulbDanger;
using Njord.Features.Alerts.WindChill;
using Njord.Messages.Results;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Alerts;

public sealed class AlertWindowHoursTests
{
    private const int FullWindow = 24;
    private const int ShortWindow = 12;

    private static IOptions<T> Opts<T>(T value) where T : class => Options.Create(value);

    private static DomainForecast<ForecastPoint> AtHour20(Func<ForecastPoint, ForecastPoint> change) =>
        Day((h, p) => h == 20 ? change(p) : p);

    private static void AssertShortWindowExcludesHour20<TInput>(Func<int, FeatureRule<TInput>> create, DomainForecast<ForecastPoint> weather)
        where TInput : struct
    {
        var forecast = ForecastWithEnsemble(weather, EnsembleDay());
        Assert.Multiple(
            () => Assert.NotNull(Run(create(FullWindow), Now, forecast)),
            () => Assert.Null(Run(create(ShortWindow), Now, forecast)));
    }

    [Fact]
    public void Frost_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new FrostAlert(Opts(new FrostConfig { Enabled = true, WindowHours = hours, Thresholds = FrostConfig.DefaultThresholds })),
            AtHour20(p => p with { Temperature = -10.0 }));

    [Fact]
    public void Heat_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new HeatAlert(Opts(new HeatConfig { Enabled = true, WindowHours = hours, Thresholds = HeatConfig.DefaultThresholds })),
            AtHour20(p => p with { Temperature = 40.0 }));

    [Fact]
    public void Humidity_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new HumidityAlert(Opts(new HumidityConfig { Enabled = true, WindowHours = hours, Thresholds = HumidityConfig.DefaultThresholds })),
            AtHour20(p => p with { IsDay = 1, DewPoint = 24.0 }));

    [Fact]
    public void Storm_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new StormAlert(Opts(new StormConfig { Enabled = true, WindowHours = hours, GustThresholds = StormConfig.DefaultGustThresholds })),
            AtHour20(p => p with { WindGusts = 40.0 }));

    [Fact]
    public void Snow_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new SnowAlert(Opts(new SnowConfig { Enabled = true, WindowHours = hours, Thresholds = SnowConfig.DefaultThresholds })),
            AtHour20(p => p with { Snowfall = 16.0 }));

    [Fact]
    public void Wind_chill_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new WindChillAlert(Opts(new WindChillConfig { Enabled = true, WindowHours = hours, Thresholds = WindChillConfig.DefaultThresholds })),
            AtHour20(p => p with { ApparentTemperature = -30.0 }));

    [Fact]
    public void Ice_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new IceAlert(Opts(new IceConfig { Enabled = true, WindowHours = hours })),
            AtHour20(p => p with { Precipitation = 1.0, Temperature = 0.0 }));

    [Fact]
    public void Visibility_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new VisibilityAlert(Opts(new VisibilityConfig { Enabled = true, WindowHours = hours, Thresholds = VisibilityConfig.DefaultThresholds })),
            AtHour20(p => p with { Visibility = 49.0 }));

    [Fact]
    public void Tropical_night_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new TropicalNightAlert(Opts(new TropicalNightConfig { Enabled = true, WindowHours = hours, Thresholds = TropicalNightConfig.DefaultThresholds })),
            AtHour20(p => p with { IsDay = 0, Temperature = 26.0 }));

    [Fact]
    public void Clear_sky_uv_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new ClearSkyUvAlert(Opts(new ClearSkyUvConfig { Enabled = true, WindowHours = hours, Thresholds = ClearSkyUvConfig.DefaultThresholds })),
            AtHour20(p => p with { UvIndexClearSky = 12.0, IsDay = 1.0 }));

    [Fact]
    public void Wet_bulb_danger_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new WetBulbDangerAlert(Opts(new WetBulbDangerConfig { Enabled = true, WindowHours = hours, Thresholds = WetBulbDangerConfig.DefaultThresholds })),
            AtHour20(p => p with { WetBulbTemperature = 33.0 }));

    [Fact]
    public void Pressure_drop_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new PressureDropAlert(Opts(new PressureDropConfig { Enabled = true, WindowHours = hours })),
            Day((h, p) => p with { Pressure = h < 18 ? 1013.0 : 1000.0 }));

    [Fact]
    public void Fog_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new FogAlert(Opts(new FogConfig { Enabled = true, WindowHours = hours })),
            AtHour20(p => p with { Temperature = 5.0, DewPoint = 4.5, WindSpeed = 1.0, Humidity = 98.0 }));

    [Fact]
    public void Heavy_rain_short_window_excludes_hour_20() =>
        AssertShortWindowExcludesHour20(
            hours => new HeavyRainAlert(Opts(new HeavyRainConfig { Enabled = true, WindowHours = hours })),
            AtHour20(p => p with { Precipitation = 30.0 }));

    [Fact]
    public void Dust_short_window_excludes_hour_20()
    {
        var forecast = new LocationForecast(
            Lucerne,
            AirQuality: Njord.Tests.Shared.AirQualityFixture.Day((h, p) => h == 20 ? p with { Dust = 500.0 } : p));
        DustAlert Create(int hours) => new(Opts(new DustConfig { Enabled = true, WindowHours = hours, Thresholds = DustConfig.DefaultThresholds }));

        Assert.Multiple(
            () => Assert.NotNull(Run(Create(FullWindow), Now, forecast)),
            () => Assert.Null(Run(Create(ShortWindow), Now, forecast)));
    }
}
