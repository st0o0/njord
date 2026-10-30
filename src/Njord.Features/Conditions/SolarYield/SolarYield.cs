using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Messages.Results;
using Njord.Messages.Results.Conditions;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.SolarYield;

public sealed class SolarYield(IOptions<SolarYieldConfig> config) : FeatureRule<ForecastInput>
{
    private const double SunnierFactor = 1.2;
    private const double CloudierFactor = 0.8;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "SolarYield",
        new DomainRequirements(Forecast: ForecastParams.GlobalTiltedIrradiance));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastInput? Project(LocationForecast forecast) =>
        forecast.Forecast is { } w ? new(w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastInput> ctx)
    {
        var hours = ForecastQueries.Upcoming(ctx.Current.Forecast, ctx.Now, 24)
            .Where(point => point.GlobalTiltedIrradiance is not null)
            .Select(point => (point.ValidFrom, Radiation: point.GlobalTiltedIrradiance!.Value))
            .ToList();
        if (hours.Count == 0)
        {
            return null;
        }

        var options = config.Value;
        var kwhPerSquareMeter = hours.Sum(hour => hour.Radiation) / 1000.0;
        var yield = kwhPerSquareMeter * options.SystemEfficiency * options.PeakKw;
        var peak = hours.MaxBy(hour => hour.Radiation);
        return new SolarYieldResult(ctx.Location, Math.Round(yield, 2), peak.ValidFrom.Hour);
    }

    protected override IReadOnlyList<PipelineEvent> EvaluateAll(FeatureContext<ForecastInput> ctx)
    {
        List<PipelineEvent> events = [];
        if (Evaluate(ctx) is { } yield)
        {
            events.Add(yield);
        }

        if (config.Value.BatteryCapacityKwh > 0 && EvaluateBattery(ctx) is { } battery)
        {
            events.Add(battery);
        }

        return events;
    }

    // Remaining hours of today and tomorrow: forecast GTI, irradiance on the panel (W/m2).
    private SolarBatteryStrategyConditionResult? EvaluateBattery(FeatureContext<ForecastInput> ctx)
    {
        var options = config.Value;
        var today = ctx.Now.UtcDateTime.Date;
        var tomorrow = today.AddDays(1);

        var todayWatts = ctx.Current.Forecast.Points
            .Where(point => point.ValidFrom.UtcDateTime.Date == today && point.GlobalTiltedIrradiance is not null)
            .Select(point => point.GlobalTiltedIrradiance!.Value)
            .ToList();
        var tomorrowWatts = ctx.Current.Forecast.Points
            .Where(point => point.ValidFrom.UtcDateTime.Date == tomorrow && point.GlobalTiltedIrradiance is not null)
            .Select(point => point.GlobalTiltedIrradiance!.Value)
            .ToList();
        if (todayWatts.Count == 0 || tomorrowWatts.Count == 0)
        {
            return null;
        }

        var todayKwh = ToKwh(todayWatts, options);
        var tomorrowKwh = ToKwh(tomorrowWatts, options);
        return new SolarBatteryStrategyConditionResult(
            ctx.Location,
            Decide(todayKwh, tomorrowKwh, options.BatteryCapacityKwh),
            Math.Round(todayKwh, 2),
            Math.Round(tomorrowKwh, 2));
    }

    public static BatteryStrategy Decide(double todayKwh, double tomorrowKwh, double batteryCapacityKwh)
    {
        var surplus = todayKwh >= batteryCapacityKwh;
        if (tomorrowKwh > todayKwh * SunnierFactor)
        {
            return surplus ? BatteryStrategy.FeedIn : BatteryStrategy.Conserve;
        }

        return surplus && tomorrowKwh < todayKwh * CloudierFactor ? BatteryStrategy.ChargeBattery : BatteryStrategy.Normal;
    }

    private static double ToKwh(List<double> watts, SolarYieldConfig options) =>
        watts.Sum(w => w * options.PeakKw / 1000.0 * options.SystemEfficiency);
}
