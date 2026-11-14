using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Shared;
using Njord.Messages.Results;
using Njord.Messages.Results.Scores;
using Njord.Messages.Stream;

namespace Njord.Features.Scores.SleepQuality;

public sealed class SleepQualityScore(IOptions<SleepQualityConfig> config) : FeatureRule<ForecastAqInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Score,
        FeatureCategory.Lifestyle,
        Name: "SleepQuality",
        new DomainRequirements(
            Forecast: ForecastParams.Temperature | ForecastParams.Humidity
                | ForecastParams.WindGusts | ForecastParams.Cape | ForecastParams.Precipitation,
            AirQuality: AirQualityParams.BirchPollen | AirQualityParams.GrassPollen | AirQualityParams.Pm25,
            OptionalForecast: ForecastParams.ApparentTemperature),
        EntityMetadata: new EntityMetadata(EntityPlatform.Sensor, Unit: "%"));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastAqInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, AirQuality: { } aq } ? new ForecastAqInput(w, aq) : null;

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

    internal static bool IsNight(SleepQualityConfig cfg, int hour) =>
        cfg.NightStartHour <= cfg.NightEndHour
            ? hour >= cfg.NightStartHour && hour < cfg.NightEndHour
            : hour >= cfg.NightStartHour || hour < cfg.NightEndHour;

    internal static bool IsPreSleep(SleepQualityConfig cfg, int hour)
    {
        var hoursBeforeNight = (((cfg.NightStartHour - hour) % 24) + 24) % 24;
        return hoursBeforeNight is >= 1 and var h && h <= cfg.PreSleepHours;
    }

    internal static bool HighPollenPresent(SleepQualityConfig cfg, AirQualityPoint? air) =>
        air is not null && Math.Max(air.BirchPollen ?? 0.0, air.GrassPollen ?? 0.0) >= cfg.HighPollen;

    internal static double NightScore(SleepQualityConfig cfg, ForecastPoint weather, AirQualityPoint? air)
    {
        var score = 100.0;
        if (weather.Temperature is { } temperature)
        {
            score -= temperature switch
            {
                _ when temperature > cfg.HotThreshold => cfg.HotPenalty,
                _ when temperature > cfg.WarmThreshold => cfg.WarmPenalty,
                _ when temperature < cfg.ColdThreshold => cfg.ColdPenalty,
                _ => 0.0,
            };
        }

        if (weather.Humidity > cfg.HumidityThreshold)
        {
            score -= cfg.HumidityPenalty;
        }

        if (air?.Pm25 > cfg.Pm25Limit)
        {
            score -= cfg.Pm25Penalty;
        }

        if (HighPollenPresent(cfg, air))
        {
            score -= cfg.PollenSensitive ? cfg.PollenSensitivePenalty : cfg.PollenPenalty;
        }

        if (weather.WindGusts > cfg.GustLimit)
        {
            score -= cfg.GustPenalty;
        }

        if (weather is { Cape: { } cape, Precipitation: > 0.0 } && cape > cfg.CapeLimit)
        {
            score -= cfg.ThunderstormPenalty;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    internal static double VentilationScore(SleepQualityConfig cfg, ForecastPoint weather, AirQualityPoint? air)
    {
        var score = 100.0;
        if (weather.Temperature > cfg.HotThreshold)
        {
            score -= cfg.VentilationHotPenalty;
        }

        if (weather.Humidity > cfg.HumidityThreshold)
        {
            score -= cfg.VentilationHumidityPenalty;
        }

        if (weather.WindGusts > cfg.GustLimit)
        {
            score -= cfg.VentilationGustPenalty;
        }

        if (weather.Precipitation > 0.1)
        {
            score -= cfg.VentilationRainPenalty;
        }

        if (air?.Pm25 > cfg.Pm25Limit)
        {
            score -= cfg.VentilationPm25Penalty;
        }

        if (HighPollenPresent(cfg, air))
        {
            score -= cfg.PollenSensitive ? cfg.VentilationPollenSensitivePenalty : cfg.VentilationPollenPenalty;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }
}
