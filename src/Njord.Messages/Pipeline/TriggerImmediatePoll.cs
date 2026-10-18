using Njord.Model;

namespace Njord.Messages.Pipeline;

// A null location or an empty model triggers every configured location or model.
public sealed record TriggerImmediatePoll(LocationId? Location, string Model);

public abstract record TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollCompleted(int Count, IReadOnlyList<string> Targets) : TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollFailed(Exception Cause) : TriggerImmediatePollResponse;
