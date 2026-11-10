using Njord.Messages.Results;

namespace Njord.Grpc;

public abstract record ConsumerEvent;

public sealed record ConsumerSnapshot(IReadOnlyList<EntityState> States) : ConsumerEvent;

public sealed record EntityStateChanged(string EntityId, FeatureResult Result, DateTimeOffset UpdatedAt) : ConsumerEvent;

public sealed record EntityBecameUnavailable(string EntityId, DateTimeOffset Since) : ConsumerEvent;

public sealed record EntityBecameAvailable(string EntityId, DateTimeOffset RecoveredAt) : ConsumerEvent;

internal sealed record ConsumerKeepAlive : ConsumerEvent;
