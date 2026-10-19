using Njord.Model;

namespace Njord.Messages.Alerts;

public sealed record AlertResult(
    Location Location,
    IReadOnlyList<Alert> Alerts);
