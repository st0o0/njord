using Njord.Weather;

namespace Njord.Messages.Alerts;

public sealed record AlertResult(
    LocationId Location,
    IReadOnlyList<Alert> Alerts);
