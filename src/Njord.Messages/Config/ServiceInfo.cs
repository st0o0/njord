using Njord.Messages.Pipeline;

namespace Njord.Messages.Config;

// What the running service serves, fixed at startup: its catalog, request budget and enabled enrichments.
public sealed record ServiceInfo(
    CatalogResult Catalog,
    int MonthlyBudgetLimit,
    int DailyBudgetLimit,
    IReadOnlyList<EnrichmentKind> ActiveEnrichments);
