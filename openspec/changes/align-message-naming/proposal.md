## Why

The message response naming deviates from the project's own VerbNoun/QueryNoun
convention in 8 response hierarchies. Query responses use ad-hoc names like
`BudgetResponse` instead of `QueryBudgetUsageResponse`, making the codebase
inconsistent and harder to navigate. Since the project is 0.x, renaming is a
breaking change that costs nothing now but becomes expensive after 1.0.

## What Changes

- Rename 8 query-response hierarchies to match their query names:
  - `BudgetResponse` → `QueryBudgetUsageResponse`, `BudgetUsageResult` → `QueryBudgetUsageResult`, `BudgetResponseFailed` → `QueryBudgetUsageFailed`
  - `SchedulerQueryResponse` → `QueryPollStatesResponse`, `PollStatesResult` → `QueryPollStatesResult`, `SchedulerQueryFailed` → `QueryPollStatesFailed`
  - `SensorSnapshotQueryResponse` → `QuerySensorSnapshotResponse` (and subtypes)
  - `ForecastQueryResponse` → `QueryForecastResponse` (and subtypes)
  - `AllForecastsQueryResponse` → `QueryAllForecastsResponse` (and subtypes)
  - `EnrichmentQueryResponse` → `QueryEnrichmentResponse` (and subtypes)
  - `AllEnrichmentsQueryResponse` → `QueryAllEnrichmentsResponse` (and subtypes)
  - `HistoryQueryResponse` → `QueryHistoryResponse` (and subtypes in Enrichment)
- `TriggerPollResult` → convert to proper hierarchy with `TriggerImmediatePollResponse` base
- Update all consumer sites (actors, gRPC services, tests)

## Capabilities

### New Capabilities

- `message-response-naming`: Align all query-response hierarchies to the
  `QueryNounResponse` / `QueryNounResult` / `QueryNounFailed` naming pattern.

### Modified Capabilities

(none)

## Non-goals

- Moving messages from feature libraries (Mqtt, Enrichment) into Njord.Messages
  — that is an architectural decision tracked separately.
- Changing command naming (already correct VerbNoun).
- Adding response hierarchies to fire-and-forget commands.

## Impact

- **Files changed:** Message files in `src/Njord.Messages/`, one in
  `src/Njord.Enrichment/`, plus all consumer sites (actors, gRPC services, tests).
- **API budget:** Zero — no polling or HTTP changes.
- **Risk:** Medium — rename touches many files but is mechanical. 0.x allows
  breaking changes. `refactor!:` commit prefix.
