using Njord.Core;
using Njord.Core.Forecast;

namespace Njord.Messages.Stream;

public sealed record DomainDataReceived(Location Location, DomainKind Domain, IDomainForecast Data, string? ModelId = null);
