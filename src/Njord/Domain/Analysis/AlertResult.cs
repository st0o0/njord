namespace Njord.Domain.Analysis;

public sealed record AlertResult(string Location, IReadOnlyList<Alert> Alerts);
