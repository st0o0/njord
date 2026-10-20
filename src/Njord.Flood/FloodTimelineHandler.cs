using Njord.Core.Domain;
using Njord.Forecast.Flood;
using Njord.Messages.Pipeline;

namespace Njord.Flood;

public sealed class FloodTimelineHandler : IDomainHandler
{
    public FeatureDescriptor Feature { get; } = new(
        FeatureKind.FloodTimeline,
        FeatureCategory.Flood,
        "Discharge timeline for the coming days",
        DefaultEnabled: false,
        new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeP75));

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Flood is not { } flood || flood.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        if (flood.Points.MaxBy(point => point.Discharge) is not { Discharge: { } peakDischarge } peak)
        {
            return [];
        }

        var daysAbove = flood.Points.Count(point => point.DischargeP75 is { } warning && point.Discharge > warning);
        return [new PipelineEvent.FloodTimelineComputed(current.Location, peak.ValidFrom, peakDischarge, daysAbove)];
    }
}
