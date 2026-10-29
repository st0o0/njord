using Njord.Core.Forecast;

namespace Njord.Core.Domain;

public sealed record DomainDataReceived(Location Location, DomainKind Domain, IDomainForecast Data, string? ModelId = null);
