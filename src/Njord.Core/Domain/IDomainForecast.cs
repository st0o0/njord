using Njord.Model;

namespace Njord.Core.Domain;

public interface IDomainForecast
{
    DomainKind Domain { get; }
    DateTimeOffset ModelRun { get; }
    TimeResolution Resolution { get; }
    DateTimeOffset FirstValid { get; }
    DateTimeOffset LastValid { get; }
}
