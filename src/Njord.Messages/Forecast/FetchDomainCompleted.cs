using Njord.Model;

namespace Njord.Messages.Forecast;

public sealed record FetchDomainCompleted(LocationId Location, DomainKind Domain, int Hash) : FetchDomainResponse;
