namespace Njord.Persistence.Events.Polling;

public enum PersistedDomainPollOutcome
{
    Changed = 0,
    Unchanged = 1,
    Failed = 2,
}
