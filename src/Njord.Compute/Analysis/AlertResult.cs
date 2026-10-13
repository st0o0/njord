using Njord.Weather;

namespace Njord.Compute.Analysis;

public sealed record AlertResult(
    LocationId Location,
    IReadOnlyList<Alert> Alerts);
