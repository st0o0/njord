using System.Collections.Immutable;

namespace Njord.Core.Forecast;

public sealed record DomainForecast<T>(
    DomainKind Domain,
    DateTimeOffset ModelRun,
    TimeResolution Resolution,
    ImmutableArray<T> Points) : IDomainForecast
    where T : IHasValidTime
{
    public DateTimeOffset FirstValid => Points.IsDefaultOrEmpty ? default : Points[0].ValidFrom;

    public DateTimeOffset LastValid => Points.IsDefaultOrEmpty ? default : Points[^1].ValidFrom;

    public int ContentHash()
    {
        var hash = new HashCode();
        hash.Add(Domain);
        hash.Add(Resolution);
        foreach (var p in Points)
        {
            hash.Add(p);
        }
        return hash.ToHashCode();
    }
}
