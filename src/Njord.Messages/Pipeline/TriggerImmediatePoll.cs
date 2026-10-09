namespace Njord.Messages.Pipeline;

public sealed record TriggerImmediatePoll(string Location, string Model);

public abstract record TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollCompleted(int Count, List<string> Targets) : TriggerImmediatePollResponse;
public sealed record TriggerImmediatePollFailed(Exception Cause) : TriggerImmediatePollResponse;
