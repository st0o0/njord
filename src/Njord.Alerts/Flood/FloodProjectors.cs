using Njord.Core.Domain;
using Njord.Model;

namespace Njord.Alerts.Flood;

public sealed class FloodRiskProjector : IProjector<FloodRiskInput>
{
    public IReadOnlySet<DomainKind> RequiredDomains { get; } = new HashSet<DomainKind> { DomainKind.Flood };

    public FloodRiskInput? Project(LocationForecast current, LocationForecast? previous) =>
        current.Flood is { } flood
            ? new FloodRiskInput(current.Location, flood)
            : null;
}

public sealed class FloodRainProjector : IProjector<FloodRainInput>
{
    public IReadOnlySet<DomainKind> RequiredDomains { get; } =
        new HashSet<DomainKind> { DomainKind.Flood, DomainKind.Weather };

    public FloodRainInput? Project(LocationForecast current, LocationForecast? previous) =>
        current.Flood is { } flood && current.Weather is { } weather
            ? new FloodRainInput(current.Location, flood, weather)
            : null;
}
