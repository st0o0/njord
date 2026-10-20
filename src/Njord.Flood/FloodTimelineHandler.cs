using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Messages.Pipeline;

namespace Njord.Flood;

public sealed class FloodTimelineHandler : IDomainHandler
{
    public string ConfigKey => "FloodTimeline";

    public IReadOnlySet<DomainKind> Triggers { get; } = new HashSet<DomainKind> { DomainKind.Flood };

    public IReadOnlyList<PipelineEvent> Handle(LocationForecast current, LocationForecast? previous, TimeProvider time)
    {
        if (current.Flood is not { } flood || flood.Points.IsDefaultOrEmpty)
        {
            return [];
        }

        var peak = flood.Points.MaxBy(point => point.Discharge)!;
        var daysAbove = flood.Points.Count(point => point.DischargeP75 is { } warning && point.Discharge > warning);
        return [new PipelineEvent.FloodTimelineComputed(current.Location, peak.ValidFrom, peak.Discharge, daysAbove)];
    }
}
