## REMOVED Requirements

### Requirement: Completed variants match command name
**Reason**: Its `RequestForecastStream` example is removed and `FetchForecast` gains a `Rejected` variant.
**Migration**: See "Command response variants match the command name".

## ADDED Requirements

### Requirement: Command response variants match the command name
Command response variants SHALL be named `<VerbNoun>Completed`, `<VerbNoun>Failed` and, when the command can be refused, `<VerbNoun>Rejected`, matching the command record name.

#### Scenario: FetchForecast responses
- **WHEN** `FetchForecast` command is answered
- **THEN** the success type SHALL be `FetchForecastCompleted`, the failure type `FetchForecastFailed` and the refusal type `FetchForecastRejected`

#### Scenario: RequestForecastSink responses
- **WHEN** `RequestForecastSink` command completes
- **THEN** the success type SHALL be `RequestForecastSinkCompleted` and failure `RequestForecastSinkFailed`

#### Scenario: RequestPipelineOutput responses
- **WHEN** `RequestPipelineOutput` command completes
- **THEN** the success type SHALL be `RequestPipelineOutputCompleted` and failure `RequestPipelineOutputFailed`
