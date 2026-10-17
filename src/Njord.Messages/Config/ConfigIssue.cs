namespace Njord.Messages.Config;

// A null section marks an issue of the request as a whole.
public sealed record ConfigIssue(EnrichmentSection? Section, string Message);
