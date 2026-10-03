namespace Njord.Messages.Sensors;

public sealed record PushResult(bool Accepted, string? RejectionReason);
