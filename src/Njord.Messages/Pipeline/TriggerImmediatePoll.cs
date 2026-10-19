using Njord.Model;

namespace Njord.Messages.Pipeline;

// A null location triggers every configured location.
public sealed record TriggerImmediatePoll(LocationId? Location);

public abstract record TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollCompleted(int Count, IReadOnlyList<string> Targets) : TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollFailed(Exception Cause) : TriggerImmediatePollResponse;
