namespace Njord.Messages.Pipeline;

public enum EnrichmentKind
{
    Alerts,
}

public static class EnrichmentKindExtensions
{
    public static string ToWireName(this EnrichmentKind kind) => kind switch
    {
        EnrichmentKind.Alerts => "alerts",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
