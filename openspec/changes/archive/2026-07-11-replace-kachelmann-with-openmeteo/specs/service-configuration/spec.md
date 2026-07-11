# service-configuration Specification (Delta)

## ADDED Requirements

### Requirement: Request budget defaults to the Open-Meteo free tier
The system SHALL resolve a request budget of 300,000 requests/month and
600 requests/minute (Open-Meteo free-tier soft limits) when no explicit
budget is configured. All throttling and validation SHALL consume the
resolved budget.

#### Scenario: Default budget without configuration
- **WHEN** no budget is configured
- **THEN** the resolved budget is 300,000 requests/month and
  600 requests/minute

## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Plan presets resolve to request budgets
**Reason**: Kachelmann plans (`Hobby`, `BusinessStarter`, …) have no
Open-Meteo equivalent; the free tier is a single budget default, not a preset
family.
**Migration**: Configure nothing (free-tier default applies) or set an
explicit budget override.

### Requirement: API key comes from the environment only
**Reason**: Open-Meteo requires no authentication; there is no API key.
**Migration**: Remove the `Njord__ApiKey` environment variable from
deployments; startup no longer validates it.
