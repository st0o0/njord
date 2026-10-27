using Njord.Core;
using Njord.Core.Domain;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Forecast;

public sealed record QueryDomainData(Location Location, DomainKind Domain, string? ModelId = null);

public abstract record DomainDataResponse;
public sealed record DomainDataResult(PipelineEvent Event) : DomainDataResponse;
public sealed record DomainDataNotAvailable(Location Location, DomainKind Domain, string? ModelId = null) : DomainDataResponse;
