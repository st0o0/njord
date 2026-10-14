## ADDED Requirements

### Requirement: Fetch replies are attributed to the configured target
`PollScheduler` SHALL attribute every `FetchForecastCompleted` and `FetchForecastFailed` to the configured target whose location and model match the reply case-insensitively, and SHALL update, persist and schedule that target under its configured spelling. A reply that matches no configured target SHALL be logged at Warning and SHALL NOT create, update or schedule any target.

#### Scenario: Reply spelled differently updates the configured target
- **WHEN** `lucerne` with model `icon_d2` is configured and `FetchForecastCompleted(LUCERNE, "ICON_D2", hash)` arrives
- **THEN** the poll state of (lucerne, icon_d2) is updated and no second target appears in the poll states

#### Scenario: Reply for an unknown target is ignored
- **WHEN** `FetchForecastFailed(atlantis, "icon_d2", Transport, ...)` arrives and `atlantis` is not configured
- **THEN** a warning is logged and the poll states are unchanged
