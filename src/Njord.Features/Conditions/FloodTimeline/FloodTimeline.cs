using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Forecast.Flood;
using Njord.Messages.Pipeline;

namespace Njord.Features.Conditions.FloodTimeline;

public sealed class FloodTimeline(IOptions<FloodTimelineConfig> config) : FeatureRule<FloodWeatherInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Flood,
        DefaultEnabled: false,
        new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeP75));

    public override bool Enabled => config.Value.Enabled;

    protected override FloodWeatherInput? Project(LocationForecast forecast) =>
        forecast is { Flood: { } f, Weather: { } w } ? new(f, w) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<FloodWeatherInput> ctx)
    {
        var flood = ctx.Current.Flood;
        if (flood.Points.IsDefaultOrEmpty)
        {
            return null;
        }

        if (flood.Points.MaxBy(point => point.Discharge) is not { Discharge: { } peakDischarge } peak)
        {
            return null;
        }

        var daysAbove = flood.Points.Count(point => point.DischargeP75 is { } warning && point.Discharge > warning);
        return new FloodTimelineResult(ctx.Location, daysAbove > 0 ? "above_warning" : "normal", FeatureData.Of(
            ("peak_time", FeatureData.Time(peak.ValidFrom)),
            ("peak_discharge", FeatureData.Number(peakDischarge)),
            ("days_above_warning", FeatureData.Number(daysAbove))));
    }
}
