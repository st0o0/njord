using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Core.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Features.Alerts.FloodRain;

public sealed class FloodRainAlert(IOptions<FloodRainConfig> config) : FeatureRule<FloodForecastInput>
{
    public const int ForecastDays = 7;
    public const double HeavyRain24HourMillimeters = 30.0;

    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Alert,
        FeatureCategory.Flood,
        Name: "FloodRain",
        DefaultEnabled: false,
        new DomainRequirements(Forecast: ForecastParams.Precipitation,
            Flood: FloodParams.Discharge | FloodParams.DischargeMedian | FloodParams.DischargeP75));

    public override bool Enabled => config.Value.Enabled;

    protected override FloodForecastInput? Project(LocationForecast forecast) =>
        forecast is { Flood: { } f, Forecast: { } w } ? new(f, w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<FloodForecastInput> ctx)
    {
        var rain = ctx.Current.Forecast.NextHoursInclusive(ctx.Now).Sum(p => p.Precipitation);
        if (rain < HeavyRain24HourMillimeters)
        {
            return null;
        }

        var points = ctx.Current.Flood.NextDays(ctx.Now, ForecastDays).Where(p => p.Discharge is not null).ToList();
        if (points.MaxBy(p => p.Discharge) is not { Discharge: { } peakDischarge } peak)
        {
            return null;
        }

        var rising = peakDischarge > points[0].Discharge;
        if (!rising || peak.DischargeMedian is not { } median || peakDischarge <= median)
        {
            return null;
        }

        var aboveP75 = peak.DischargeP75 is { } p75 && peakDischarge > p75;
        var severity = aboveP75 ? AlertSeverity.Red : AlertSeverity.Orange;
        return new FloodRainAlertResult(
            ctx.Location, severity, 1.0,
            Math.Round(peakDischarge, 1),
            Math.Round(aboveP75 ? peak.DischargeP75!.Value : median, 1),
            PeakValue: null, HoursUntil: peak.HoursFrom(ctx.Now), DurationHours: null, Level: ConfidenceLevel.High);
    }
}
