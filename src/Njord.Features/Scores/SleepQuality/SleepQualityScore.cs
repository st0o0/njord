using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.SleepQuality;

public sealed class SleepQualityScore(IOptions<SleepQualityConfig> config) : FeatureRule<ForecastAqInput>
{
    public const double HighPollen = 50.0;
    public const double Pm25Limit = 25.0;
    public const double GustLimit = 11.1;
    public const double CapeLimit = 1000.0;
    public const int PreSleepHours = 6;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "SleepQuality",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.Humidity
                | ForecastParams.WindGusts | ForecastParams.Cape | ForecastParams.Precipitation,
            AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen | AirQualityParams.Pm25,
            OptionalForecast: ForecastParams.ApparentTemperature));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new(w, aq) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastAqInput> ctx)
    {
        var cfg = config.Value;
        var airByTime = ctx.Current.AirQuality.NextHours(ctx.Now).ToDictionary(p => p.ValidFrom);
        var night = new List<double>();
        var preSleep = new List<HourlyScore>();
        DateTimeOffset? firstNight = null;
        foreach (var weather in ctx.Current.Forecast.NextHours(ctx.Now))
        {
            airByTime.TryGetValue(weather.ValidFrom, out var air);
            var hour = weather.ValidFrom.UtcDateTime.Hour;
            if (IsNight(cfg, hour))
            {
                firstNight ??= weather.ValidFrom;
                night.Add(NightScore(cfg, weather, air));
            }
            else if (IsPreSleep(cfg, hour))
            {
                preSleep.Add(new HourlyScore(weather.ValidFrom, VentilationScore(cfg, weather, air)));
            }
        }

        if (night.Count == 0)
        {
            return null;
        }

        var score = (int)Math.Round(night.Average());
        var windows = WindowScanner.Scan(preSleep, cfg.MinWindowScore, cfg.MinWindowHours, cfg.MaxWindows);
        if (windows.Count == 0)
        {
            return new SleepQualityScoreResult(ctx.Location, score, firstNight ?? ctx.Now, 0);
        }

        var best = windows[0];
        return new SleepQualityScoreResult(ctx.Location, score, best.Start, (int)(best.End - best.Start).TotalHours)
        {
            Windows = windows,
        };
    }

    private static bool IsNight(SleepQualityConfig cfg, int hour) =>
        cfg.NightStartHour <= cfg.NightEndHour
            ? hour >= cfg.NightStartHour && hour < cfg.NightEndHour
            : hour >= cfg.NightStartHour || hour < cfg.NightEndHour;

    private static bool IsPreSleep(SleepQualityConfig cfg, int hour)
    {
        var hoursBeforeNight = (((cfg.NightStartHour - hour) % 24) + 24) % 24;
        return hoursBeforeNight is >= 1 and <= PreSleepHours;
    }

    private static bool HighPollenPresent(AirQualityPoint? air) =>
        air is not null && Math.Max(air.BirchPollen ?? 0.0, air.GrassPollen ?? 0.0) >= HighPollen;

    private static double NightScore(SleepQualityConfig cfg, ForecastPoint weather, AirQualityPoint? air)
    {
        var score = 100.0;
        if (weather.Temperature is { } temperature)
        {
            score -= temperature switch
            {
                > 25.0 => 30.0,
                > 22.0 => 15.0,
                < 5.0 => 10.0,
                _ => 0.0,
            };
        }

        if (weather.Humidity > 80.0)
        {
            score -= 10.0;
        }

        if (air?.Pm25 > Pm25Limit)
        {
            score -= 15.0;
        }

        if (HighPollenPresent(air))
        {
            score -= cfg.PollenSensitive ? 20.0 : 5.0;
        }

        if (weather.WindGusts > GustLimit)
        {
            score -= 10.0;
        }

        if (weather.Cape > CapeLimit && weather.Precipitation > 0.0)
        {
            score -= 15.0;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    private static double VentilationScore(SleepQualityConfig cfg, ForecastPoint weather, AirQualityPoint? air)
    {
        var score = 100.0;
        if (weather.Temperature > 25.0)
        {
            score -= 30.0;
        }

        if (weather.Humidity > 80.0)
        {
            score -= 20.0;
        }

        if (weather.WindGusts > GustLimit)
        {
            score -= 15.0;
        }

        if (weather.Precipitation > 0.1)
        {
            score -= 30.0;
        }

        if (air?.Pm25 > Pm25Limit)
        {
            score -= 40.0;
        }

        if (HighPollenPresent(air))
        {
            score -= cfg.PollenSensitive ? 40.0 : 15.0;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }
}
