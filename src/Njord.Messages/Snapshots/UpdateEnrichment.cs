namespace Njord.Messages.Snapshots;

public sealed record UpdateEnrichment(string Location, string TypeName, object Result) : IWithEnrichmentKey;
