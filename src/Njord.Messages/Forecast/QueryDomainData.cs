using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record QueryDomainData(LocationId Location, DomainKind Domain);

public abstract record DomainDataResponse;
public sealed record DomainDataResult(PipelineEvent Event) : DomainDataResponse;
public sealed record DomainDataNotAvailable(LocationId Location, DomainKind Domain) : DomainDataResponse;
