using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record QueryDomainData(Location Location, DomainKind Domain);

public abstract record DomainDataResponse;
public sealed record DomainDataResult(PipelineEvent Event) : DomainDataResponse;
public sealed record DomainDataNotAvailable(Location Location, DomainKind Domain) : DomainDataResponse;
