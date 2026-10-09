namespace Njord.Messages.Snapshots;

public sealed record QueryAllEnrichments(string Location) : IWithLocation;

public abstract record QueryAllEnrichmentsResponse;
public sealed record QueryAllEnrichmentsCompleted(IReadOnlyList<(string TypeName, object Value)> Results) : QueryAllEnrichmentsResponse;
public sealed record QueryAllEnrichmentsFailed(Exception Cause) : QueryAllEnrichmentsResponse;
