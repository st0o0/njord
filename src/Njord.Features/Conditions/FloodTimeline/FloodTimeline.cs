using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Messages.Pipeline;
using Njord.Messages.Results;
using Njord.Messages.Stream;

namespace Njord.Features.Conditions.FloodTimeline;

public sealed class FloodTimeline(IOptions<FloodTimelineConfig> config) : FeatureRule<FloodInput>
{
    public override FeatureDescriptor Descriptor { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Flood,
        Name: "FloodTimeline",
        DefaultEnabled: false,
        new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeP75));

    public override bool Enabled => config.Value.Enabled;

    protected override FloodInput? Project(LocationForecast forecast) =>
        forecast is { Flood: { } f } ? new(f) : null;

    protected override PipelineEvent? Evaluate(FeatureContext<FloodInput> ctx)
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
        return new FloodTimelineResult(ctx.Location, daysAbove > 0 ? FloodStatus.AboveWarning : FloodStatus.Normal, peak.ValidFrom, peakDischarge, daysAbove);
    }
}
