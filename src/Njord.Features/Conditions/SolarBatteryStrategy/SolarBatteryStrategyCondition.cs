using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Core.Solar;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.SolarBatteryStrategy;

public sealed class SolarBatteryStrategyCondition(IOptions<SolarBatteryStrategyConfig> config) : FeatureRule<ForecastSolarInput>
{

    private const double SunnierFactor = 1.2;
    private const double CloudierFactor = 0.8;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Energy,
        Name: "SolarBatteryStrategy",
        DefaultEnabled: false,
        new DomainRequirements(
            Forecast: ForecastParams.ShortwaveRadiation,
            Satellite: SatelliteParams.GlobalTilted));

    public override bool Enabled => config.Value.Enabled;

    protected override ForecastSolarInput? Project(LocationForecast forecast) =>
        forecast is { Forecast: { } w, Satellite: { } s } ? new(w, s) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<ForecastSolarInput> ctx)
    {
        var options = config.Value;
        var today = ctx.Now.UtcDateTime.Date;
        var tomorrow = today.AddDays(1);

        var todayWatts = ctx.Current.Satellite.Points
            .Where(point => point.ValidFrom.UtcDateTime.Date == today && point.ValidFrom <= ctx.Now && point.GTI is not null)
            .Select(point => point.GTI!.Value)
            .ToList();
        var tomorrowWatts = ctx.Current.Forecast.Points
            .Where(point => point.ValidFrom.UtcDateTime.Date == tomorrow && point.ShortwaveRadiation is not null)
            .Select(point => point.ShortwaveRadiation!.Value)
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

    private static double ToKwh(List<double> watts, SolarBatteryStrategyConfig options) =>
        watts.Sum(w => w * options.PeakKw / 1000.0 * options.SystemEfficiency);
}
