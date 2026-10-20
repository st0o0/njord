using Njord.Forecast;

namespace Njord.Messages.Pipeline;

// A null location triggers every configured location.
public sealed record TriggerImmediatePoll(Location? Location);

public abstract record TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollCompleted(int Count, IReadOnlyList<string> Targets) : TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollFailed(Exception Cause) : TriggerImmediatePollResponse;
