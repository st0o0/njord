namespace Njord.Messages.Pipeline;

public enum EnrichmentKind
{
    Consensus,
    Alerts,
    Derived,
    Trends,
    Indices,
}

public static class EnrichmentKindExtensions
{
    public static string ToWireName(this EnrichmentKind kind) =>
        kind.ToString().ToLowerInvariant();
}
