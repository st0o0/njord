using System.Collections.Immutable;
using Njord.Model;

namespace Njord.Core.Domain;

public sealed record DomainForecast<T>(
    DomainKind Domain,
    DateTimeOffset ModelRun,
    TimeResolution Resolution,
    ImmutableArray<T> Points) : IDomainForecast
    where T : IHasValidTime
{
    public DateTimeOffset FirstValid => Points.IsDefaultOrEmpty ? default : Points[0].ValidFrom;

    public DateTimeOffset LastValid => Points.IsDefaultOrEmpty ? default : Points[^1].ValidFrom;
}
