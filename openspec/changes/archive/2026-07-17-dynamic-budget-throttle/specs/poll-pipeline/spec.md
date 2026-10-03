## MODIFIED Requirements

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests using a `BudgetThrottleStage` that derives its rate from `IBudgetProvider.GetCurrentRate()`. The stage SHALL implement weighted token-bucket throttling based on the effective budget (default: 80% of `RequestsPerMinute`). The stage SHALL adapt to budget changes at runtime without re-materializing the graph. The fetch call SHALL use `SelectAsyncUnordered(2)` to limit concurrent connections to Open-Meteo to 2.

#### Scenario: Throttle rate derived from budget
- **WHEN** the effective budget is 600 req/min (free tier)
- **THEN** the stage SHALL throttle at 480 weighted cost-units per minute (80% politeness margin)

#### Scenario: Budget override changes throttle rate at runtime
- **WHEN** the user sets a `BudgetOverride` of 60 req/min via gRPC
- **THEN** the stage SHALL adapt to 48 cost-units per minute within 5 seconds

#### Scenario: Maximum 2 concurrent HTTP calls
- **WHEN** the throttle releases 2 targets within the same second
- **THEN** `SelectAsyncUnordered(2)` processes both concurrently but does not start a third until one completes

#### Scenario: Fetch is inlined in the pipeline graph
- **WHEN** the PipelineActor materializes the pipeline
- **THEN** the fetch logic is a direct `SelectAsyncUnordered` call to `IOpenMeteoClient.FetchAsync`, not a separate `FetchStage.Create()` flow
