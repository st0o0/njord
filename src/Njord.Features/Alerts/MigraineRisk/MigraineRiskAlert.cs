using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Alerts.MigraineRisk;

public sealed class MigraineRiskAlert(IOptions<MigraineRiskConfig> config) : FeatureRule<ForecastInput>
{
    private const int ShortLookbackHours = 6;
    private const int LongLookbackHours = 24;
    private const int FoehnLookbackHours = 3;
    private const double FoehnDirectionMin = 150.0;
    private const double FoehnDirectionMax = 210.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Forecast,
        Name: "MigraineRisk",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.Pressure | ForecastParams.Temperature | ForecastParams.Humidity
                | ForecastParams.DewPoint | ForecastParams.WindDirection),
        PollDepth: ShortLookbackHours);

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var cfg = config.Value;
        var nowHour = ctx.Now.HourStart();
        if (At(ctx, nowHour) is not { } current || current.Pressure is not { } pressureNow)
        {
            return null;
        }

        var drop6h = Drop(ctx, nowHour, pressureNow, ShortLookbackHours);
        var drop24h = Drop(ctx, nowHour, pressureNow, LongLookbackHours);

        var level = 0;
        var trigger = 0.0;
        var threshold = cfg.PressureThreshold6h;
        if (drop6h >= cfg.PressureThreshold6h)
        {
            level = 1;
            trigger = drop6h;
        }

        if (drop24h >= cfg.PressureThreshold24h)
        {
            level = 2;
            trigger = drop24h;
            threshold = cfg.PressureThreshold24h;
        }

        if (level > 0)
        {
            if (Swing(ctx, nowHour, static p => p.Temperature) > cfg.TempSwingThreshold)
            {
                level++;
            }

            if (Swing(ctx, nowHour, static p => p.Humidity) > cfg.HumiditySwingThreshold)
            {
                level++;
            }
        }

        if (IsFoehn(ctx, nowHour, current))
        {
            level += 2;
        }

        if (level == 0)
        {
            return null;
        }

        var severity = Math.Min(level, 3) switch
        {
            1 => AlertSeverity.Yellow,
            2 => AlertSeverity.Orange,
            _ => AlertSeverity.Red,
        };
        var confidence = ConfidenceLevel.Medium;
        return new MigraineRiskAlertResult(
            ctx.Location, severity, confidence.ToConfidence(), Math.Round(trigger, 1), threshold,
            PeakValue: null, HoursUntil: null, DurationHours: null, Level: confidence);
    }

    // Looks up the point valid at the given hour, newest forecast first. Older forecasts and the past hours of
    // the current one both count, so the lookback is not limited to PollDepth.
    private static ForecastPoint? At(FeatureContext<ForecastInput> ctx, DateTimeOffset hour)
    {
        foreach (var input in ctx.History)
        {
            foreach (var point in input.Forecast.Points)
            {
                if (point.ValidFrom == hour)
                {
                    return point;
                }
            }
        }

        return null;
    }

    private static double Drop(FeatureContext<ForecastInput> ctx, DateTimeOffset nowHour, double pressureNow, int hours) =>
        At(ctx, nowHour.AddHours(-hours))?.Pressure is { } earlier ? earlier - pressureNow : 0.0;

    private static double Swing(FeatureContext<ForecastInput> ctx, DateTimeOffset nowHour, Func<ForecastPoint, double?> selector)
    {
        double? min = null;
        double? max = null;
        for (var h = 0; h <= LongLookbackHours; h++)
        {
            if (At(ctx, nowHour.AddHours(-h)) is { } point && selector(point) is { } value)
            {
                min = min is null ? value : Math.Min(min.Value, value);
                max = max is null ? value : Math.Max(max.Value, value);
            }
        }

        return max - min ?? 0.0;
    }

    private static bool IsFoehn(FeatureContext<ForecastInput> ctx, DateTimeOffset nowHour, ForecastPoint current)
    {
        if (current is not { WindDirection: { } direction, Temperature: { } temperature, Humidity: { } humidity, Pressure: { } pressure }
            || direction is < FoehnDirectionMin or > FoehnDirectionMax)
        {
            return false;
        }

        return At(ctx, nowHour.AddHours(-FoehnLookbackHours)) is { Temperature: { } t0, Humidity: { } h0, Pressure: { } p0 }
            && temperature > t0 && humidity < h0 && pressure > p0;
    }
}
