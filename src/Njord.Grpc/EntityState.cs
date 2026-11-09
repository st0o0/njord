using Njord.Messages.Results;

namespace Njord.Grpc;

internal sealed record EntityState(
    string EntityId,
    FeatureResult Result,
    DateTimeOffset LastUpdated,
    bool IsAvailable = true);
