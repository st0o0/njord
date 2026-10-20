using Njord.Core.Configuration;
using Njord.Forecast;

namespace Njord.Core.Domain;

public static class EnrichmentResolution
{
    public static IReadOnlyList<IDomainHandler> EnabledHandlers(EnrichmentOptions options, IEnumerable<IDomainHandler> handlers) =>
        [.. handlers.Where(handler => options.IsEnabled(handler.ConfigKey))];

    public static DomainRequirements RequiredParams(EnrichmentOptions options, IEnumerable<IDomainHandler> handlers) =>
        EnabledHandlers(options, handlers).Aggregate(default(DomainRequirements), (all, handler) => all | handler.Requirements);

    public static IReadOnlySet<DomainKind> RequiredDomains(EnrichmentOptions options, IEnumerable<IDomainHandler> handlers)
    {
        var requirements = RequiredParams(options, handlers);
        return Enum.GetValues<DomainKind>().Where(requirements.Requires).ToHashSet();
    }
}
