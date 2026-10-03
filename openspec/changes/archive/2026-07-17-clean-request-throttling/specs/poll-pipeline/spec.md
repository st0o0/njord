## MODIFIED Requirements

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests using a fixed politeness rate of **2 elements per second** with a maximum burst of 4, using `ThrottleMode.Shaping`. The throttle SHALL be element-count-based (not weighted). The fetch call SHALL use `SelectAsyncUnordered(2)` to limit concurrent connections to Open-Meteo to 2.

#### Scenario: Throttle rate is 2 per second
- **WHEN** 27 weight-1 targets enter the pipeline simultaneously at startup
- **THEN** the throttle shapes them to 2 per second (with an initial burst of up to 4), completing all 27 in approximately 14 seconds

#### Scenario: Throttle is independent of budget configuration
- **WHEN** the user sets a `BudgetOverride` of 60 req/min
- **THEN** the pipeline throttle rate remains at 2 req/sec (the budget override affects monthly/minute accounting, not the politeness throttle)

#### Scenario: Maximum 2 concurrent HTTP calls
- **WHEN** the throttle releases 2 targets within the same second
- **THEN** `SelectAsyncUnordered(2)` processes both concurrently but does not start a third until one completes

#### Scenario: Fetch is inlined in the pipeline graph
- **WHEN** the PipelineActor materializes the pipeline
- **THEN** the fetch logic is a direct `SelectAsyncUnordered` call to `IOpenMeteoClient.FetchAsync`, not a separate `FetchStage.Create()` flow
