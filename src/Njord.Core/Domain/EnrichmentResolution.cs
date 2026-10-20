using Njord.Core.Configuration;
using Njord.Forecast;

namespace Njord.Core.Domain;

public static class EnrichmentResolution
{
    public static IReadOnlyList<IDomainHandler> EnabledHandlers(EnrichmentOptions options, IEnumerable<IDomainHandler> handlers) =>
        [.. handlers.Where(handler => options.IsEnabled(handler.ConfigKey))];

    public static IReadOnlySet<DomainKind> RequiredDomains(EnrichmentOptions options, IEnumerable<IDomainHandler> handlers) =>
        EnabledHandlers(options, handlers).SelectMany(handler => handler.Triggers).ToHashSet();
}
