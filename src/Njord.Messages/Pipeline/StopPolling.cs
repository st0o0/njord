namespace Njord.Messages.Pipeline;

public sealed record StopPolling(string TargetId) : IWithTargetId;
