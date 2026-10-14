namespace Njord.Messages.Pipeline;

public sealed record TriggerTargetPoll(string TargetId) : IWithTargetId;
