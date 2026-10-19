using Njord.Model;

namespace Njord.Core.Domain;

public sealed record DomainDataReceived(Location Location, DomainKind Domain, IDomainForecast Data);
