using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchDomain(LocationId Location, DomainKind Domain);

public abstract record FetchDomainResponse;
