## REMOVED Requirements

### Requirement: Query responses SHALL follow QueryNounResponse naming
**Reason**: Contradicts the code and the other naming specs. Query responses drop the `Query` prefix (`BudgetUsageResponse`/`BudgetUsageResult`/`BudgetUsageFailed`, `PollStatesResponse`/`PollStatesResult`/`PollStatesFailed`, `ForecastResponse`/`ForecastResult`/`ForecastNotAvailable`, src/Njord.Messages), and `QueryAllForecasts`, `QueryEnrichment`, `QueryAllEnrichments` and `QueryHistory` no longer exist.
**Migration**: The query-response convention is defined by `message-conventions` ("Response hierarchies with Completed and Failed") and `message-naming-convention` ("Query responses drop the Query prefix").
