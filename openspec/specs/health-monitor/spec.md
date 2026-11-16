# health-monitor Specification

## Purpose

Health state for ASP.NET Core health checks is owned by the feature actors and read through query messages; each domain publishes its health slice as one complete immutable result.
## Requirements
### Requirement: Feature health state is owned by the feature actor
Each feature that exposes health SHALL own its health state in its own actor and answer a query message with a complete immutable result (`QueryPollHealth` in Polling, `QueryBudgetStatus` / `BudgetStatusResult` in Budget). The owning actor SHALL be the only writer; the feature's health check SHALL be the only reader and SHALL ask the actor. `Njord.Core` SHALL NOT declare health state types.

#### Scenario: Poll health reflects the last successful poll
- **WHEN** a poll completes at timestamp T
- **THEN** the answer to `QueryPollHealth` reports T

#### Scenario: Core has no health types
- **WHEN** `Njord.Core` is inspected
- **THEN** it declares no `NjordHealthMonitor`, `PollHealth` or `BudgetHealth`
