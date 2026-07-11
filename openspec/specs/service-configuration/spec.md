# service-configuration Specification

## Purpose

Configuration and startup validation for the service: Open-Meteo free-tier request budget defaults with an optional override, monthly budget projection guards, and minimal viable configuration defaults.

## Requirements

### Requirement: Request budget defaults to the Open-Meteo free tier
The system SHALL resolve a request budget of 300,000 requests/month and
600 requests/minute (Open-Meteo free-tier soft limits) when no explicit
budget is configured. All throttling and validation SHALL consume the
resolved budget.

#### Scenario: Default budget without configuration
- **WHEN** no budget is configured
- **THEN** the resolved budget is 300,000 requests/month and
  600 requests/minute

### Requirement: Budget override supersedes the preset
The system SHALL accept an optional budget override (requests/month,
requests/minute) that replaces the default free-tier values entirely, so users
can self-throttle below the soft limits.

#### Scenario: Override wins over default
- **WHEN** an override of 50,000 requests/month and 60 requests/minute is
  configured
- **THEN** the resolved budget is 50,000 requests/month and
  60 requests/minute

### Requirement: Startup validation enforces the budget projection
The system SHALL project monthly usage as
`locations × models × cycles-per-month` (cycles derived from the poll
interval) and SHALL refuse to start when the projection exceeds 80 % of the
resolved monthly request budget, reporting the projection and the limit in the
error.

#### Scenario: Default configuration passes
- **WHEN** 1 location, 8 models, and a 60-minute poll interval are configured
  with the default budget
- **THEN** the projection is ≈ 5,760 requests/month and startup proceeds

#### Scenario: Over-budget configuration is rejected
- **WHEN** 2 locations, 8 models, and a 60-minute poll interval are configured
  with an override budget of 10,000 requests/month
- **THEN** startup fails, reporting a projection of ≈ 11,520 against the
  8,000 (80 %) guard

### Requirement: Minimal viable configuration is enforced
The system SHALL require at least one location (name, latitude, longitude) and
at least one non-empty model id, and SHALL default the poll interval to
60 minutes when unspecified.

#### Scenario: Empty model list is rejected
- **WHEN** the configuration contains a location but no models
- **THEN** startup validation fails naming the empty model list

#### Scenario: Poll interval defaults
- **WHEN** no poll interval is configured
- **THEN** the effective poll interval is 60 minutes
