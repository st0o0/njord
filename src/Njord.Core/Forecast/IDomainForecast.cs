
namespace Njord.Core.Forecast;

public interface IDomainForecast
{
    DomainKind Domain { get; }
    DateTimeOffset ModelRun { get; }
    TimeResolution Resolution { get; }
    DateTimeOffset FirstValid { get; }
    DateTimeOffset LastValid { get; }
}
