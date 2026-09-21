## Purpose

Defines message naming conventions (Query* prefix for queries) and response hierarchy patterns (abstract base with Completed/Failed subtypes) across all actor APIs.

## Requirements

### Requirement: Query messages use Query prefix
All actor query messages SHALL use the `Query*` naming convention. Messages that request state without causing side effects SHALL be named `Query<Noun>`.

#### Scenario: Budget query naming
- **WHEN** a caller requests current budget usage from BudgetTrackerActor
- **THEN** the message type is `QueryBudgetUsage` (not `GetBudgetUsage`)

#### Scenario: Poll states query naming
- **WHEN** a caller requests poll states from SchedulerActor
- **THEN** the message type is `QueryPollStates` (not `GetPollStates`)

#### Scenario: Forecast query naming
- **WHEN** a caller requests a single forecast from ForecastSnapshotActor
- **THEN** the message type is `QueryForecast` (not `GetForecast`)

#### Scenario: All forecasts query naming
- **WHEN** a caller requests all forecasts from ForecastSnapshotActor
- **THEN** the message type is `QueryAllForecasts` (not `GetAllForecasts`)

#### Scenario: Enrichment query naming
- **WHEN** a caller requests a single enrichment from EnrichmentSnapshotActor
- **THEN** the message type is `QueryEnrichment` (not `GetEnrichment`)

#### Scenario: All enrichments query naming
- **WHEN** a caller requests all enrichments from EnrichmentSnapshotActor
- **THEN** the message type is `QueryAllEnrichments` (not `GetAllEnrichments`)

#### Scenario: Sensor snapshot query naming
- **WHEN** a caller requests the latest sensor reading from SensorHubActor
- **THEN** the message type is `QuerySensorSnapshot` (not `GetSnapshot`)

#### Scenario: Existing Query prefix preserved
- **WHEN** ForecastHistoryActor receives a history query
- **THEN** the message type remains `QueryHistory` (already correct)

### Requirement: Response hierarchies with Completed and Failed
Actor query and command responses SHALL use abstract base records with typed subtypes. Each response domain SHALL have a `Failed` subtype carrying `Exception Cause`.

#### Scenario: Budget response hierarchy
- **WHEN** BudgetTrackerActor responds to `QueryBudgetUsage`
- **THEN** the response is either `BudgetUsageResult` (success) or `BudgetResponseFailed(Exception Cause)`

#### Scenario: Forecast query response hierarchy
- **WHEN** ForecastSnapshotActor responds to `QueryForecast`
- **THEN** the response is `ForecastFound(ModelForecast)`, `ForecastNotFound(string ModelKey)`, or `ForecastQueryFailed(Exception Cause)`

#### Scenario: Enrichment query response hierarchy
- **WHEN** EnrichmentSnapshotActor responds to `QueryEnrichment`
- **THEN** the response is `EnrichmentFound(object Result)`, `EnrichmentNotFound(string Key)`, or `EnrichmentQueryFailed(Exception Cause)`

#### Scenario: Callers use pattern matching
- **WHEN** a gRPC service handles a forecast response
- **THEN** it uses a `switch` expression or pattern match over the response subtypes instead of null-checking

#### Scenario: Failed carries exception
- **WHEN** an actor encounters an exception while handling a query
- **THEN** it responds with the domain's `*Failed` record containing the caught `Exception` as `Cause`

### Requirement: Deduplicated Ack type
The `Ack` acknowledgement record SHALL be defined in exactly one location and imported by all consumers. Duplicate `Ack` definitions across namespaces SHALL be removed.

#### Scenario: Single Ack definition
- **WHEN** searching the codebase for `record Ack`
- **THEN** exactly one definition exists, shared by all actors that use acknowledgement responses

### Requirement: CLAUDE.md message pattern documentation
CLAUDE.md SHALL document the message organisation as "Pattern B (co-located with actors)" to match the actual project layout.

#### Scenario: Accurate documentation
- **WHEN** reading the CLAUDE.md message convention section
- **THEN** it states Pattern B (co-located) and does not reference a dedicated Messages project
