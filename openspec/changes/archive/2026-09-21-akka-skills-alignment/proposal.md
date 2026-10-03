## Why

The persistent actors (SchedulerActor, BudgetTrackerActor, ForecastSnapshotActor, ForecastHistoryActor, EnrichmentSnapshotActor) hold mutable private fields with inline recovery logic, making business logic hard to test in isolation. Message naming is inconsistent (`Get*` vs `Query*`), and response types use ad-hoc patterns (nullable, bool flags) instead of structured hierarchies. Aligning with akka-skills best practices makes actors thinner, state logic unit-testable as pure functions, and the message API consistent and self-documenting.

## What Changes

- **Immutable state records**: Replace mutable private fields in all 5 persistent actors with immutable state records and `Apply()` extension methods (pure functions). Actors become thin shells that delegate to state logic.
- **Three-tier persistence model**: Introduce Internal State → Snapshot (caller-facing) → Persisted (journal DTO) separation. Recovery via `State.FromPersistence(snapshot)`. Existing extend-only DTOs remain the Persisted tier unchanged.
- **Query naming**: Rename all `Get*` query messages to `Query*` (`GetPollStates` → `QueryPollStates`, `GetBudgetUsage` → `QueryBudgetUsage`, `GetForecast` → `QueryForecast`, `GetAllForecasts` → `QueryAllForecasts`, `GetEnrichment` → `QueryEnrichment`, `GetAllEnrichments` → `QueryAllEnrichments`, `GetSnapshot` → `QuerySensorSnapshot`).
- **Response hierarchies**: Introduce abstract response bases with `Completed`/`Failed` subtypes for command and query responses. `Failed` variants carry `Exception Cause`. Replace nullable/bool response patterns.
- **CLAUDE.md fix**: Correct "Pattern A (dedicated Messages project)" to "Pattern B (co-located)" to match the actual layout.
- **State-only unit tests**: Add pure-function tests for each state record's `Apply()` methods — no actor system needed.

## Non-goals

- Extracting a dedicated Messages project — co-located messages are appropriate for a single-service app.
- Changing persistence DTOs — existing extend-only DTOs stay as-is (they are the Persisted tier).
- Changing actor supervision topology — BackoffSupervisor wrapping is already correct.
- Modifying stream graphs or pipeline behavior.
- Adding new features or changing any runtime behavior — this is a structural refactoring.

## Capabilities

### New Capabilities

- `actor-state-pattern`: Immutable state records with Apply()/GetSnapshot() extensions and three-tier persistence model for all persistent actors.
- `message-conventions`: Query* naming convention and abstract response hierarchies with Completed/Failed subtypes across all actor message APIs.

### Modified Capabilities

_None — this is an internal refactoring that does not change spec-level behavior or requirements._

## Impact

- **Code**: All 5 persistent actors rewritten as thin shells. All message files updated (renames + new response types). All callers updated (gRPC services, other actors, tests).
- **Tests**: Existing actor/persistence tests updated for renamed messages and new response types. New state-only unit tests added. All 620+ existing tests must stay green.
- **API budget**: No impact — no polling changes.
- **Breaking**: None externally. Internal message renames are compile-time breaking within the solution — no public API surface.
