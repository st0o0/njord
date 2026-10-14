## REMOVED Requirements

### Requirement: Persistence event DataChanged has unique name
**Reason**: `PersistedDataChanged` and the message `Njord.Messages.Pipeline.DataChanged` are removed together with the single poll scheduler.
**Migration**: The poll worker's events are covered by "Poll worker persistence records have unique names".

## ADDED Requirements

### Requirement: Poll worker persistence records have unique names
The poll worker's persistence records (`PollingStarted`, `PollingStopped`, `ForecastChanged`, `ForecastUnchanged`,
`PollFailed`, `PollDeferred`, `PersistedPollWorkerState`) SHALL have names that no public type in `Njord.Messages`,
`Njord.Core` or `Njord.Pipeline` uses, so code referencing both the messages and the persistence records needs no
`using` alias.

#### Scenario: No ambiguity in projects referencing both Messages and Persistence
- **GIVEN** `Njord.Pipeline` references both `Njord.Messages` and `Njord.Persistence`
- **WHEN** it uses the poll worker's events and the polling messages (`StartPolling`, `StopPolling`, `TriggerTargetPoll`, `PollWorkerReport`)
- **THEN** every name resolves without a `using` alias
