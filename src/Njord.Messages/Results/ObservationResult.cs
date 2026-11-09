using Njord.Core;

namespace Njord.Messages.Results;

public sealed record ObservationResult(
    Location Location,
    string FeatureName,
    double Value) : FeatureResult(Location);
