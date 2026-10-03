## ADDED Requirements

### Requirement: Query responses SHALL follow QueryNounResponse naming

Every query message `QueryNoun` SHALL have a response hierarchy named
`QueryNounResponse` (abstract base), with `QueryNounResult` (success) and
`QueryNounFailed` (failure) subtypes. The base type name SHALL match the query
name with `Response` appended.

#### Scenario: Budget query response follows naming convention
- **WHEN** the system defines `QueryBudgetUsage`
- **THEN** its responses are `QueryBudgetUsageResponse` (abstract), `QueryBudgetUsageResult`, `QueryBudgetUsageFailed`

#### Scenario: Poll states query response follows naming convention
- **WHEN** the system defines `QueryPollStates`
- **THEN** its responses are `QueryPollStatesResponse` (abstract), `QueryPollStatesResult`, `QueryPollStatesFailed`

#### Scenario: Sensor snapshot query response follows naming convention
- **WHEN** the system defines `QuerySensorSnapshot`
- **THEN** its responses are `QuerySensorSnapshotResponse` (abstract) with appropriate subtypes

#### Scenario: Snapshot query responses follow naming convention
- **WHEN** the system defines `QueryForecast`, `QueryAllForecasts`, `QueryEnrichment`, `QueryAllEnrichments`
- **THEN** each has `Query<Noun>Response` / `Query<Noun>Result` / `Query<Noun>Failed`

#### Scenario: History query response follows naming convention
- **WHEN** the system defines `QueryHistory`
- **THEN** its responses are `QueryHistoryResponse` (abstract), `QueryHistoryResult` (or `ForecastHistoryResult`), `QueryHistoryFailed`

### Requirement: Command responses with results SHALL follow VerbNounResponse naming

Commands that return a result (not fire-and-forget) SHALL have a response
named `VerbNounResponse` matching the command name.

#### Scenario: TriggerImmediatePoll has proper response hierarchy
- **WHEN** the system defines `TriggerImmediatePoll`
- **THEN** its response base is `TriggerImmediatePollResponse` (not `TriggerPollResult`)
