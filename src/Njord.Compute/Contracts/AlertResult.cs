using Njord.Weather;

namespace Njord.Compute.Contracts;

public sealed record AlertResult(
    LocationId Location,
    IReadOnlyList<Alert> Alerts);
