using Njord.Core;

namespace Njord.Messages.Polling;

public sealed record TriggerImmediatePoll(Location? Location);

public abstract record TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollCompleted(int Count, IReadOnlyList<string> Targets) : TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollFailed(Exception Cause) : TriggerImmediatePollResponse;
