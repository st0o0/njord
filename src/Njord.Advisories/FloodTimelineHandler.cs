using Njord.Core.Domain;
using Njord.Forecast.Flood;
using Njord.Messages.Pipeline;

namespace Njord.Advisories;

public sealed class FloodTimelineHandler : IDomainHandler
{
    public string Name => "FloodTimeline";

    public FeatureDescriptor Feature { get; } = new(
        OutputKind.Condition,
        FeatureCategory.Flood,
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
        return [new ConditionResult(current.Location, this.WireKey(), daysAbove > 0 ? "above_warning" : "normal", FeatureData.Of(
            ("peak_time", FeatureData.Time(peak.ValidFrom)),
            ("peak_discharge", FeatureData.Number(peakDischarge)),
            ("days_above_warning", FeatureData.Number(daysAbove))))];
    }
}
