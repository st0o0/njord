# server-status-api Specification

## Purpose

`GetStatus` RPC on `ConfigService` exposing server health, version, uptime,
budget consumption counters, and per-model fetch status so operators and
dashboards can monitor njord without scraping logs.

## Requirements

### Requirement: GetStatus returns server health and budget usage
`ConfigService.GetStatus` SHALL return a `ServerStatus` message containing server version, uptime in seconds, budget usage (monthly/daily limits and used counts), and per-model fetch status.

#### Scenario: Status includes version and uptime
- **WHEN** a client calls `GetStatus`
- **THEN** the response SHALL contain the server version from assembly metadata and uptime since service start

#### Scenario: Budget usage shows monthly and daily counts
- **WHEN** a client calls `GetStatus` after njord has fetched data
- **THEN** the `BudgetStatus` SHALL show monthly_used, daily_used, and corresponding limits

#### Scenario: Per-model status shows fetch state
- **WHEN** a client calls `GetStatus` with active models
- **THEN** each `ModelStatus` SHALL show location, model, last_fetch_timestamp, consecutive_failures, and state ("active", "stale", "error", or "disabled")
