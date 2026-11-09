using Njord.Core;

namespace Njord.Messages.Results;

public sealed record ObservationResult(
    Location Location,
    double Value) : FeatureResult(Location);
