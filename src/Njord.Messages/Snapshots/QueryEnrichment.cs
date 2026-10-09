namespace Njord.Messages.Snapshots;

public sealed record QueryEnrichment(string Location, string TypeName) : IWithEnrichmentKey;

public abstract record QueryEnrichmentResponse;
public sealed record QueryEnrichmentCompleted(object Result) : QueryEnrichmentResponse;
public sealed record QueryEnrichmentNotAvailable(string Key) : QueryEnrichmentResponse;
public sealed record QueryEnrichmentFailed(Exception Cause) : QueryEnrichmentResponse;
