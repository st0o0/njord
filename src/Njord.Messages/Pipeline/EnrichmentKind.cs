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
    public static string ToWireName(this EnrichmentKind kind) => kind switch
    {
        EnrichmentKind.Consensus => "consensus",
        EnrichmentKind.Alerts => "alerts",
        EnrichmentKind.Derived => "derived",
        EnrichmentKind.Trends => "trends",
        EnrichmentKind.Indices => "indices",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
